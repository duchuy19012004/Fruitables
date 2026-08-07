using System.Reflection;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Communications;
using Fruitables.Services.Pricing.ProductPricing;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class PriceScheduleWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Worker_reindexes_products_with_historical_price_state_without_replaying_notifications()
    {
        await using var context = CreateContext();
        context.PriceSchedules.Add(new PriceSchedule
        {
            ProductId = 1,
            StartsAt = Now.AddDays(-2),
            EndsAt = Now.AddDays(-1),
            DiscountType = DiscountType.Percentage,
            Value = 10
        });
        await context.SaveChangesAsync();

        var notifier = new Mock<IRealtimeNotifier>();
        var indexing = new Mock<IIndexingService>();

        await ProcessOnceAsync(context, notifier.Object, indexing.Object, Now, Now, isStartupCheck: true);

        notifier.Verify(service => service.NotifyPriceChangedAsync(
            It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
        indexing.Verify(service => service.IndexProductAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_reindexes_each_product_once_for_multiple_variant_transitions()
    {
        await using var context = CreateContext();
        context.PriceSchedules.AddRange(
            new PriceSchedule
            {
                ProductId = 1,
                ProductVariantId = 10,
                StartsAt = Now.AddHours(-1),
                DiscountType = DiscountType.Percentage,
                Value = 10
            },
            new PriceSchedule
            {
                ProductId = 1,
                ProductVariantId = 11,
                StartsAt = Now.AddHours(-1),
                DiscountType = DiscountType.Percentage,
                Value = 20
            });
        await context.SaveChangesAsync();

        var notifier = new Mock<IRealtimeNotifier>();
        var indexing = new Mock<IIndexingService>();

        await ProcessOnceAsync(context, notifier.Object, indexing.Object, Now.AddHours(-2), Now);

        notifier.Verify(service => service.NotifyPriceChangedAsync(1, It.IsAny<int?>()), Times.Exactly(2));
        indexing.Verify(service => service.IndexProductAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task ProcessOnceAsync(
        ApplicationDbContext context,
        IRealtimeNotifier notifier,
        IIndexingService indexing,
        DateTimeOffset lastCheck,
        DateTimeOffset now,
        bool isStartupCheck = false)
    {
        var method = typeof(PriceScheduleWorker).GetMethod(
            "ProcessTransitionsAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = method!.Invoke(null, [context, notifier, indexing, lastCheck, now, CancellationToken.None, isStartupCheck]);
        await Assert.IsAssignableFrom<Task>(task);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
