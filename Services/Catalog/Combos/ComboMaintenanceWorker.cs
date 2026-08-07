using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Catalog.Combos;

public sealed class ComboMaintenanceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ComboMaintenanceWorker> _logger;
    private readonly IJsonDocumentSerializer _serializer;

    public ComboMaintenanceWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<ComboMaintenanceWorker> logger,
        IJsonDocumentSerializer? serializer = null)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), _timeProvider);
        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Combo cart maintenance failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var staleBefore = now.UtcDateTime.AddHours(-24);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var promotions = await db.Promotions.AsNoTracking()
            .Where(promotion => promotion.Type == "combo")
            .ToListAsync(cancellationToken);
        var payloads = promotions
            .Select(promotion =>
                (Promotion: promotion, Payload: _serializer.Deserialize<ComboPayload>(promotion.PayloadJson)))
            .ToList();

        var carts = await db.Carts
            .OrderBy(cart => cart.Id)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var removedGroups = 0;
        foreach (var cart in carts)
        {
            if (string.IsNullOrWhiteSpace(cart.LinesJson) || cart.LinesJson.Trim() == "[]")
                continue;

            CartLinesDocument document;
            try
            {
                document = _serializer.Deserialize<CartLinesDocument>(cart.LinesJson);
            }
            catch
            {
                continue;
            }

            var invalidGroupIds = document.Lines
                .Where(line => line.CartGroupId.HasValue && line.ComboId.HasValue)
                .GroupBy(line => line.CartGroupId!.Value)
                .Where(group =>
                {
                    var first = group.First();
                    if (!TryResolveCombo(payloads, first.ComboId!.Value, out var combo))
                        return true;
                    if (first.ComboRevision != combo.Payload.Revision)
                        return cart.UpdatedAt <= staleBefore;
                    return !combo.Promotion.IsActive ||
                           !combo.Payload.IsActive ||
                           combo.Payload.Status is ComboLifecycleStatus.Draft or ComboLifecycleStatus.Archived ||
                           (combo.Payload.EndsAt.HasValue && combo.Payload.EndsAt <= now);
                })
                .Select(group => group.Key)
                .ToHashSet();

            if (invalidGroupIds.Count == 0)
                continue;

            var remaining = document.Lines.Where(line => !line.CartGroupId.HasValue || !invalidGroupIds.Contains(line.CartGroupId.Value)).ToList();
            cart.LinesJson = _serializer.Serialize(document.With(lines: remaining));
            cart.UpdatedAt = DateTime.UtcNow;
            cart.RowVersion = Guid.NewGuid().ToByteArray();
            removedGroups += invalidGroupIds.Count;
        }

        if (removedGroups == 0)
            return 0;

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Removed {Count} expired or invalid combo cart groups.", removedGroups);
        return removedGroups;
    }

    private static bool TryResolveCombo(
        IReadOnlyCollection<(Promotion Promotion, ComboPayload Payload)> promotions,
        int comboId,
        out (Promotion Promotion, ComboPayload Payload) combo)
    {
        combo = promotions.FirstOrDefault(item =>
            string.Equals(item.Promotion.Code, $"combo:{comboId}", StringComparison.OrdinalIgnoreCase) ||
            item.Promotion.Id == comboId);
        return combo.Promotion != null;
    }
}
