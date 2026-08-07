using Fruitables.Data;
using Fruitables.Models.Json;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Chat.Knowledge;

namespace Fruitables.Services.Pricing.ProductPricing;

public sealed class PriceScheduleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PriceScheduleWorker> _logger;
    private readonly IJsonDocumentSerializer _serializer;

    public PriceScheduleWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<PriceScheduleWorker> logger,
        IJsonDocumentSerializer? serializer = null)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first pass replays every historical boundary once so a restart after
        // extended downtime always repairs realtime/catalog and chatbot state.
        var lastCheck = DateTimeOffset.MinValue;
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
                var promotions = await db.Promotions.AsNoTracking()
                    .Where(promotion => promotion.Type == "price-schedule")
                    .ToListAsync(stoppingToken);
                var transitions = promotions
                    .Select(promotion => _serializer.Deserialize<PriceSchedulePayload>(promotion.PayloadJson))
                    .Where(schedule =>
                        (!schedule.IsCancelled &&
                            ((schedule.StartsAt > lastCheck && schedule.StartsAt <= now) ||
                             (schedule.EndsAt.HasValue && schedule.EndsAt > lastCheck && schedule.EndsAt <= now))) ||
                        (schedule.IsCancelled && schedule.CancelledAt.HasValue &&
                            schedule.CancelledAt > lastCheck && schedule.CancelledAt <= now))
                    .Select(schedule => new { schedule.ProductId, schedule.ProductVariantId })
                    .Distinct().ToList();
                foreach (var transition in transitions)
                {
                    await notifier.NotifyPriceChangedAsync(transition.ProductId, transition.ProductVariantId);
                    await indexing.IndexProductAsync(transition.ProductId, stoppingToken);
                }
                lastCheck = now;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Price schedule transition processing failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
