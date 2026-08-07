using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.Services.Catalog.Combos;

namespace Fruitables.Tests;

public class ComboOperationsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Mock<IProductPricingService> Pricing(decimal price = 100m)
    {
        var mock = new Mock<IProductPricingService>();
        mock.Setup(service => service.GetQuotesAsync(It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) => targets.Distinct().ToDictionary(
                target => target,
                target => new PriceQuote(target.ProductId, target.ProductVariantId, price, price, null)));
        return mock;
    }

    private static Product Product(int id) => new()
    {
        Id = id,
        Name = $"Sản phẩm {id}",
        Slug = $"san-pham-{id}",
        Price = 100,
        StockQuantity = 20,
        MinOrderQuantity = 1,
        IsActive = true
    };

    [Fact]
    public void Lifecycle_applies_start_inclusive_and_end_exclusive()
    {
        var combo = new Combo
        {
            IsActive = true,
            Status = ComboLifecycleStatus.Scheduled,
            StartsAt = Now,
            EndsAt = Now.AddHours(1)
        };

        Assert.False(combo.IsAvailableAt(Now.AddTicks(-1)));
        Assert.True(combo.IsAvailableAt(Now));
        Assert.True(combo.IsAvailableAt(Now.AddMinutes(59)));
        Assert.False(combo.IsAvailableAt(Now.AddHours(1)));
    }

    [Fact]
    public async Task GetActiveComboCardsAsync_hides_combo_before_schedule_and_shows_it_at_start()
    {
        var options = TestDbContextFactory.CreateInMemoryOptions();
        await using var context = new ApplicationDbContext(options);
        context.Products.AddRange(Product(1), Product(2));
        context.Combos.Add(new Combo
        {
            Id = 10,
            Name = "Combo hẹn lịch",
            Slug = "combo-hen-lich",
            Status = ComboLifecycleStatus.Scheduled,
            IsActive = true,
            StartsAt = Now,
            Items =
            [
                new ComboItem { ProductId = 1, Quantity = 1 },
                new ComboItem { ProductId = 2, Quantity = 1 }
            ]
        });
        await context.SaveChangesAsync();

        var before = new ComboService(context, Pricing().Object, new FixedTimeProvider(Now.AddSeconds(-1)));
        var atStart = new ComboService(context, Pricing().Object, new FixedTimeProvider(Now));

        Assert.Empty(await before.GetActiveComboCardsAsync());
        Assert.Single(await atStart.GetActiveComboCardsAsync());
    }

    [Fact]
    public async Task UpdateAsync_rejects_stale_revision_and_audits_successful_update()
    {
        var options = TestDbContextFactory.CreateInMemoryOptions();
        await using var context = new ApplicationDbContext(options);
        context.Products.AddRange(Product(1), Product(2));
        context.Combos.Add(new Combo
        {
            Id = 10,
            Name = "Combo cũ",
            Slug = "combo-cu",
            Revision = 3,
            Status = ComboLifecycleStatus.Active,
            Items =
            [
                new ComboItem { ProductId = 1, Quantity = 1 },
                new ComboItem { ProductId = 2, Quantity = 1 }
            ]
        });
        await context.SaveChangesAsync();
        var service = new ComboService(context, Pricing().Object, new FixedTimeProvider(Now));
        var model = new ComboFormViewModel
        {
            Revision = 2,
            Name = "Combo mới",
            Status = ComboLifecycleStatus.Active,
            Items =
            [
                new ComboItemFormModel { ProductId = 1, Quantity = 1 },
                new ComboItemFormModel { ProductId = 2, Quantity = 1 }
            ]
        };

        var stale = await service.UpdateAsync(10, model);
        model.Revision = 3;
        var updated = await service.UpdateAsync(10, model);

        Assert.False(stale.Success);
        Assert.Contains("người khác cập nhật", stale.ErrorMessage);
        Assert.True(updated.Success);
        var audit = Assert.Single(await context.ComboAuditLogs.ToListAsync());
        Assert.Equal(ComboAuditActions.Update, audit.Action);
        Assert.Equal(4, audit.Revision);
    }

    [Fact]
    public async Task GetReportAsync_uses_combo_quantity_and_subtracts_refunded_lines()
    {
        var options = TestDbContextFactory.CreateInMemoryOptions();
        await using var context = new ApplicationDbContext(options);
        var delivered = new Order
        {
            Id = 1,
            OrderNumber = "D1",
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = new DateTime(2026, 7, 20),
            Items =
            [
                new OrderItem { SourceComboId = 10, ComboNameSnapshot = "Combo A", ComboRevision = 1, ComboQuantity = 2, ProductName = "A", Quantity = 2, Total = 160, ComboDiscount = 20 },
                new OrderItem { SourceComboId = 10, ComboNameSnapshot = "Combo A", ComboRevision = 1, ComboQuantity = 2, ProductName = "B", Quantity = 2, Total = 140, ComboDiscount = 20 }
            ]
        };
        var refunded = new Order
        {
            Id = 2,
            OrderNumber = "R1",
            Status = OrderStatus.Cancelled,
            PaymentStatus = PaymentStatus.Refunded,
            CreatedAt = new DateTime(2026, 7, 21),
            Items =
            [
                new OrderItem { SourceComboId = 10, ComboNameSnapshot = "Combo A", ComboRevision = 1, ComboQuantity = 1, ProductName = "A", Quantity = 1, Total = 150, ComboDiscount = 20 }
            ]
        };
        context.Orders.AddRange(delivered, refunded);
        await context.SaveChangesAsync();
        context.ReturnRequests.Add(new ReturnRequest
        {
            Id = 3,
            ReturnNumber = "RET-COMBO-PARTIAL",
            OrderId = delivered.Id,
            UserId = 1,
            Status = ReturnRequestStatus.Refunded,
            SubmittedAtUtc = new DateTime(2026, 7, 20),
            ClaimDeadlineAtUtc = new DateTime(2026, 7, 21),
            ApprovedAmount = 20m,
            Items =
            [
                new ReturnRequestItem
                {
                    OrderItemId = delivered.Items.First().Id,
                    RequestedQuantity = 0.2m,
                    ApprovedQuantity = 0.2m,
                    Reason = ReturnReasonCode.Damaged,
                    Description = "Dập",
                    RequestedAmount = 20m,
                    ApprovedAmount = 20m
                }
            ],
            Refund = new Refund
            {
                Id = 3,
                OrderId = delivered.Id,
                Amount = 20m,
                Status = RefundStatus.Succeeded,
                CreatedByUserId = 1
            }
        });
        await context.SaveChangesAsync();
        var service = new ComboService(context, Pricing().Object);

        var report = await service.GetReportAsync(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));

        var row = Assert.Single(report.Rows);
        Assert.Equal(2, row.BundlesSold);
        Assert.Equal(2, row.OrderCount);
        Assert.Equal(60m, row.ComboDiscount);
        Assert.Equal(300m, row.DeliveredRevenue);
        Assert.Equal(170m, row.RefundedRevenue);
        Assert.Equal(130m, row.NetRevenue);
    }

    [Fact]
    public async Task CleanupAsync_removes_invalid_group_after_grace_period()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Carts.Add(new Cart { Id = 1, SessionId = "expired-combo" });
            seed.Combos.Add(new Combo { Id = 10, Name = "Combo", Slug = "combo", Revision = 2, Status = ComboLifecycleStatus.Active });
            seed.CartGroups.Add(new CartGroup
            {
                Id = 20,
                CartId = 1,
                ComboId = 10,
                ComboRevision = 1,
                ComboName = "Combo",
                UpdatedAt = Now.UtcDateTime.AddDays(-2),
                ExpiresAt = Now.UtcDateTime.AddDays(10)
            });
            await seed.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        await using var provider = services.BuildServiceProvider();
        var worker = new ComboMaintenanceWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(Now),
            Mock.Of<ILogger<ComboMaintenanceWorker>>());

        Assert.Equal(1, await worker.CleanupAsync(CancellationToken.None));
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.CartGroups.ToListAsync());
    }
}
