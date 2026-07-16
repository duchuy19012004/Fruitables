using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.ViewModels;
using Xunit;

namespace Fruitables.Tests;

public class SalesAnalyticsServiceTests
{
    [Fact]
    public async Task GetHubAsync_Overview_ComputesGrossAndNetForPeriod()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        var createdAt = new DateTime(2026, 7, 5, 12, 0, 0);
        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-DEL",
                CreatedAt = createdAt,
                Total = 100m,
                Subtotal = 100m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Delivered
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-PROC",
                CreatedAt = createdAt,
                Total = 50m,
                Subtotal = 50m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Processing
            },
            new Order
            {
                Id = 3,
                OrderNumber = "ORD-REF",
                CreatedAt = createdAt,
                Total = 20m,
                Subtotal = 20m,
                PaymentStatus = PaymentStatus.Refunded,
                Status = OrderStatus.Returned
            },
            new Order
            {
                Id = 4,
                OrderNumber = "ORD-CAN",
                CreatedAt = createdAt,
                Total = 10m,
                Subtotal = 10m,
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.Cancelled
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Overview
        };

        var uow = new UnitOfWork(ctx);
        var sut = new SalesAnalyticsService(uow);
        var hub = await sut.GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.NotNull(hub.Overview);
        Assert.Equal(150, hub.Overview!.Gross.Value); // 100+50 paid
        Assert.Equal(80, hub.Overview.Net.Value);     // 100 - 20
    }
}
