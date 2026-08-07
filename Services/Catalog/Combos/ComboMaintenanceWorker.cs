using Fruitables.Data;
using Fruitables.Models;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Catalog.Combos;

public sealed class ComboMaintenanceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ComboMaintenanceWorker> _logger;

    public ComboMaintenanceWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<ComboMaintenanceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
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
                .Include(group => group.Combo)
                .OrderBy(group => group.Id)
                .Take(1000 - groupIds.Count)
                .ToListAsync(cancellationToken);
            groupIds.AddRange(staleCandidates
                .Where(group => group.ComboRevision != group.Combo.Revision ||
                    !group.Combo.IsActive ||
                    group.Combo.Status is ComboLifecycleStatus.Draft or ComboLifecycleStatus.Archived ||
                    (group.Combo.EndsAt.HasValue && group.Combo.EndsAt <= now))
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
}
