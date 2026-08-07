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

        var groupIds = await db.CartGroups
            .Where(group => group.ExpiresAt <= now.UtcDateTime)
            .OrderBy(group => group.Id)
            .Select(group => group.Id)
            .Take(1000)
            .ToListAsync(cancellationToken);

        if (groupIds.Count < 1000)
        {
            var staleCandidates = await db.CartGroups
                .AsNoTracking()
                .Where(group => group.UpdatedAt <= staleBefore && !groupIds.Contains(group.Id))
                .OrderBy(group => group.Id)
                .Take(1000 - groupIds.Count)
                .ToListAsync(cancellationToken);
            var promotions = await db.Promotions.AsNoTracking()
                .Where(promotion => promotion.Type == "combo")
                .ToListAsync(cancellationToken);
            var payloads = promotions
                .Select(promotion =>
                    (Promotion: promotion, Payload: _serializer.Deserialize<ComboPayload>(promotion.PayloadJson)))
                .ToList();
            groupIds.AddRange(staleCandidates
                .Where(group => !TryResolveCombo(payloads, group.ComboId, out var combo) ||
                    group.ComboRevision != combo.Payload.Revision ||
                    !combo.Promotion.IsActive ||
                    !combo.Payload.IsActive ||
                    combo.Payload.Status is ComboLifecycleStatus.Draft or ComboLifecycleStatus.Archived ||
                    (combo.Payload.EndsAt.HasValue && combo.Payload.EndsAt <= now))
                .Select(group => group.Id));
        }

        if (groupIds.Count == 0) return 0;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.CartItems.Where(item => item.CartGroupId.HasValue && groupIds.Contains(item.CartGroupId.Value))
            .ExecuteDeleteAsync(cancellationToken);
        await db.CartGroups.Where(group => groupIds.Contains(group.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Removed {Count} expired or invalid combo cart groups.", groupIds.Count);
        return groupIds.Count;
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
