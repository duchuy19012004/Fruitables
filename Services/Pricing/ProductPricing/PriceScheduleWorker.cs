using Fruitables.Data;
using Fruitables.Services.Communications;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Chat.Knowledge;

namespace Fruitables.Services.Pricing.ProductPricing;

public sealed class PriceScheduleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PriceScheduleWorker> _logger;

    public PriceScheduleWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<PriceScheduleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastCheck = _timeProvider.GetUtcNow();
        var isStartupCheck = true;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), _timeProvider);
        do
        {
            try
            {
                var now = _timeProvider.GetUtcNow();
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();
                var indexing = scope.ServiceProvider.GetRequiredService<IIndexingService>();
                await ProcessTransitionsAsync(db, notifier, indexing, lastCheck, now, stoppingToken, isStartupCheck);
                lastCheck = now;
                isStartupCheck = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Price schedule transition processing failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal static async Task ProcessTransitionsAsync(
        ApplicationDbContext db,
        IRealtimeNotifier notifier,
        IIndexingService indexing,
        DateTimeOffset lastCheck,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool isStartupCheck = false)
    {
        var transitions = await db.PriceSchedules.AsNoTracking()
            .Where(s => !s.IsCancelled &&
                ((s.StartsAt > lastCheck && s.StartsAt <= now) ||
                 (s.EndsAt.HasValue && s.EndsAt > lastCheck && s.EndsAt <= now)))
             .Select(s => new { s.ProductId, s.ProductVariantId })
             .Distinct()
             .ToListAsync(cancellationToken);

        var currentStateProducts = isStartupCheck
            ? await db.PriceSchedules.AsNoTracking()
                .Where(s => s.StartsAt <= now ||
                    (s.EndsAt.HasValue && s.EndsAt <= now) ||
                    (s.CancelledAt.HasValue && s.CancelledAt <= now))
                .Select(s => s.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        foreach (var transition in transitions)
            await notifier.NotifyPriceChangedAsync(transition.ProductId, transition.ProductVariantId);

        foreach (var productId in transitions.Select(transition => transition.ProductId)
                     .Concat(currentStateProducts)
                     .Distinct())
            await indexing.IndexProductAsync(productId, cancellationToken);
    }
}
