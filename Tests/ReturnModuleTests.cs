using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Returns;
using Fruitables.ViewModels;
using Fruitables.ViewModels.Returns;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class ReturnModuleTests
{
    [Fact]
    public async Task PolicyVersionCommand_CreatesNewVersionWithoutChangingOldPolicy()
    {
        await using var db = CreateContext();
        var now = Utc(2026, 7, 27);
        var old = Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, now.AddDays(-1), version: 1);
        db.ReturnPolicies.Add(old);
        await db.SaveChangesAsync();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{\"name\":\"Default v2\",\"scope\":0,\"reason\":10,\"claimWindowHours\":36,\"allowPartialRefund\":true,\"isEligible\":true}");
            var created = await new ReturnPolicyVersionCommand(db, new MutableTimeProvider(now)).CreateFromFileAsync(path);
            Assert.Equal(2, created.Version);
            Assert.Equal(36, created.ClaimWindowHours);
            Assert.Equal(24, (await db.ReturnPolicies.FindAsync(old.Id))!.ClaimWindowHours);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PolicyResolver_UsesProductThenCategoryThenDefault()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db);
        var now = Utc(2026, 7, 27);
        db.ReturnPolicies.AddRange(
            Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, now, version: 9),
            Policy(ReturnPolicyScope.Category, ReturnReasonCode.Other, 12, now, categoryId: graph.Product.CategoryId),
            Policy(ReturnPolicyScope.Product, ReturnReasonCode.Other, 6, now, productId: graph.Product.Id));
        await db.SaveChangesAsync();
        var service = new ReturnPolicyService(db);
        Assert.Equal(6, (await service.ResolveAsync(graph.Product.Id, ReturnReasonCode.Other, now))!.ClaimWindowHours);
        db.ReturnPolicies.Single(x => x.Scope == ReturnPolicyScope.Product).IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(12, (await service.ResolveAsync(graph.Product.Id, ReturnReasonCode.Other, now))!.ClaimWindowHours);
        db.ReturnPolicies.Single(x => x.Scope == ReturnPolicyScope.Category).IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(24, (await service.ResolveAsync(graph.Product.Id, ReturnReasonCode.Other, now))!.ClaimWindowHours);
    }

    [Fact]
    public async Task Eligibility_AllowsExactDeadline_ButRejectsOneTickAfter()
    {
        await using var db = CreateContext();
        var delivered = Utc(2026, 7, 26);
        var graph = SeedOrder(db, delivered);
        db.ReturnPolicies.Add(Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, delivered.AddDays(-1)));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(delivered.AddHours(24));
        var service = Eligibility(db, clock);
        Assert.True((await service.CheckItemAsync(graph.Order.Id, graph.Item.Id, graph.Customer.Id, ReturnReasonCode.Other)).Eligible);
        clock.UtcNow = delivered.AddHours(24).AddTicks(1);
        Assert.False((await service.CheckItemAsync(graph.Order.Id, graph.Item.Id, graph.Customer.Id, ReturnReasonCode.Other)).Eligible);
    }

    [Fact]
    public async Task Eligibility_RejectsOtherOwner_AndNonDeliveredOrder()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db);
        db.ReturnPolicies.Add(Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1)));
        await db.SaveChangesAsync();
        var service = Eligibility(db, new MutableTimeProvider(graph.Order.DeliveredAtUtc!.Value.AddHours(1)));
        Assert.False((await service.CheckItemAsync(graph.Order.Id, graph.Item.Id, 999, ReturnReasonCode.Other)).Eligible);
        graph.Order.Status = OrderStatus.Shipped;
        await db.SaveChangesAsync();
        Assert.False((await service.CheckItemAsync(graph.Order.Id, graph.Item.Id, graph.Customer.Id, ReturnReasonCode.Other)).Eligible);
    }

    [Fact]
    public async Task Submit_IsIdempotent_AndUsesRemainingQuantity()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db, quantity: 3);
        db.ReturnPolicies.Add(Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1)));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(graph.Order.DeliveredAtUtc!.Value.AddHours(1));
        var service = Returns(db, clock);
        var first = await service.SubmitAsync(graph.Customer.Id, Submit(graph, "same-key", 2));
        var retry = await service.SubmitAsync(graph.Customer.Id, Submit(graph, "same-key", 2));
        Assert.True(first.Success); Assert.Equal(first.Request!.Id, retry.Request!.Id);
        var exceeded = await service.SubmitAsync(graph.Customer.Id, Submit(graph, "other-key", 2));
        Assert.False(exceeded.Success);
        var remaining = await service.SubmitAsync(graph.Customer.Id, Submit(graph, "last-key", 1));
        Assert.True(remaining.Success);
    }

    [Fact]
    public async Task RefundCalculator_AllocatesCouponByCents_AndSupportsPartialComboLine()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db, quantity: 3, itemTotal: 100m);
        graph.Item.SourceComboId = 7;
        graph.Item.ComboDiscount = 20m;
        var second = new OrderItem { OrderId = graph.Order.Id, ProductId = graph.Product.Id, ProductName = "Second", Quantity = 1, BasePrice = 200, Price = 200, Total = 200 };
        graph.Order.Items.Add(second); graph.Order.Subtotal = 300; graph.Order.Discount = 10; graph.Order.Total = 290;
        await db.SaveChangesAsync();
        var result = await new RefundAmountCalculator(db).CalculateAsync(graph.Item.Id, 2);
        Assert.Equal(96.66m, result.NetPaidAmount);
        Assert.Equal(64.44m, result.RefundableAmount);
    }

    [Fact]
    public async Task RefundCalculator_SubtractsSucceededButIgnoresFailedRefunds()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db, quantity: 2, itemTotal: 100m);
        var policy = Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1));
        db.ReturnPolicies.Add(policy);
        var request = ApprovedRequest(graph, policy, 2, 100m);
        db.ReturnRequests.Add(request);
        await db.SaveChangesAsync();
        var item = request.Items.Single();
        db.Refunds.AddRange(
            new Refund { ReturnRequestId = request.Id, ReturnRequestItemId = item.Id, OrderId = graph.Order.Id, Amount = 30m, Method = RefundMethod.ManualBankTransfer, Status = RefundStatus.Succeeded, IdempotencyKey = "succeeded-refund", CreatedByUserId = graph.Admin.Id, CreatedAtUtc = Utc(2026, 7, 27) },
            new Refund { ReturnRequestId = request.Id, ReturnRequestItemId = item.Id, OrderId = graph.Order.Id, Amount = 20m, Method = RefundMethod.ManualBankTransfer, Status = RefundStatus.Failed, IdempotencyKey = "failed-refund", CreatedByUserId = graph.Admin.Id, CreatedAtUtc = Utc(2026, 7, 27) });
        await db.SaveChangesAsync();
        var result = await new RefundAmountCalculator(db).CalculateAsync(graph.Item.Id, 2);
        Assert.Equal(30m, result.PreviouslyRefundedAmount);
        Assert.Equal(70m, result.RefundableAmount);
    }

    [Fact]
    public async Task Decision_PartialApproval_RecalculatesAmountAndWritesEvent()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db, quantity: 3, itemTotal: 90m);
        db.ReturnPolicies.Add(Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1)));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(graph.Order.DeliveredAtUtc!.Value.AddHours(1));
        var service = Returns(db, clock);
        var submitted = (await service.SubmitAsync(graph.Customer.Id, Submit(graph, "partial", 3))).Request!;
        Assert.True((await service.StartReviewAsync(submitted.Id, graph.Admin.Id, Array.Empty<byte>())).Success);
        var decision = await service.DecideAsync(graph.Admin.Id, new ReturnDecisionViewModel { ReturnRequestId = submitted.Id, Reason = "Một sản phẩm còn sử dụng được", Items = { new ReturnDecisionItemViewModel { ReturnRequestItemId = submitted.Items.Single().Id, ApprovedQuantity = 2, Resolution = ReturnResolutionType.PartialRefund } } });
        Assert.True(decision.Success); Assert.Equal(ReturnRequestStatus.PartiallyApproved, decision.Request!.Status);
        Assert.Equal(60m, decision.Request.Items.Single().ApprovedAmount);
        Assert.Contains(await db.ReturnEvents.ToListAsync(), x => x.Type == ReturnEventType.PartiallyApproved);
    }

    [Theory]
    [InlineData(InventoryDispositionType.NotReturned)]
    [InlineData(InventoryDispositionType.Quarantined)]
    [InlineData(InventoryDispositionType.Discarded)]
    [InlineData(InventoryDispositionType.Donated)]
    [InlineData(InventoryDispositionType.ReturnedToSupplier)]
    public async Task FreshDisposition_NeverIncreasesSellableStock(InventoryDispositionType type)
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db);
        var policy = Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1)); policy.AllowRestock = false; db.ReturnPolicies.Add(policy);
        var request = ApprovedRequest(graph, policy, 1, 10); db.ReturnRequests.Add(request); await db.SaveChangesAsync();
        var result = await new ReturnDispositionService(db, new MutableTimeProvider(Utc(2026, 7, 27))).RecordAsync(request.Items.Single().Id, 1, type, graph.Admin.Id, "QA verified", false);
        Assert.True(result.Success); Assert.Equal(10, (await db.Products.FindAsync(graph.Product.Id))!.StockQuantity);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task ShippingFeeRefund_RequiresMerchantFaultAndWholeOrderAffected(int orderedQuantity, bool expected)
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db, quantity: orderedQuantity, itemTotal: 100m);
        graph.Order.ShippingFee = 20m;
        graph.Order.Total = 120m;
        db.ReturnPolicies.Add(Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1)));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(graph.Order.DeliveredAtUtc!.Value.AddHours(1));
        var service = Returns(db, clock);
        var submitted = (await service.SubmitAsync(graph.Customer.Id, Submit(graph, $"shipping-{orderedQuantity}", 1))).Request!;
        await service.StartReviewAsync(submitted.Id, graph.Admin.Id, Array.Empty<byte>());
        var result = await service.DecideAsync(graph.Admin.Id, new ReturnDecisionViewModel { ReturnRequestId = submitted.Id, MerchantFault = true, ApproveShippingFee = true, Items = { new ReturnDecisionItemViewModel { ReturnRequestItemId = submitted.Items.Single().Id, ApprovedQuantity = 1, Resolution = ReturnResolutionType.PartialRefund } } });
        Assert.True(result.Success);
        Assert.Equal(expected, result.Request!.ShippingFeeApproved);
    }

    [Fact]
    public async Task DeliveredAtUtc_IsWrittenOnceFromTimeProvider()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db);
        graph.Order.Status = OrderStatus.Shipped;
        graph.Order.DeliveredAtUtc = null;
        await db.SaveChangesAsync();
        var first = Utc(2026, 7, 27).AddHours(8);
        var clock = new MutableTimeProvider(first);
        var service = new OrderAdminService(db, Mock.Of<IOrderLogService>(), Mock.Of<IRealtimeNotifier>(), clock);
        Assert.True((await service.UpdateOrderStatusAsync(new UpdateOrderStatusRequest { OrderId = graph.Order.Id, NewStatus = OrderStatus.Delivered, AdminId = graph.Admin.Id })).Success);
        Assert.Equal(first, graph.Order.DeliveredAtUtc);
        clock.UtcNow = first.AddDays(1);
        await service.UpdateOrderStatusAsync(new UpdateOrderStatusRequest { OrderId = graph.Order.Id, NewStatus = OrderStatus.Delivered, AdminId = graph.Admin.Id });
        Assert.Equal(first, graph.Order.DeliveredAtUtc);
    }

    [Fact]
    public async Task ManualRefund_UpdatesProjectionWithoutChangingOrderStatus()
    {
        await using var db = CreateContext();
        var graph = SeedOrder(db, itemTotal: 100m);
        var policy = Policy(ReturnPolicyScope.Default, ReturnReasonCode.Other, 24, Utc(2026, 7, 1)); db.ReturnPolicies.Add(policy);
        var request = ApprovedRequest(graph, policy, 1, 100); db.ReturnRequests.Add(request); await db.SaveChangesAsync();
        var service = new RefundService(db, new MutableTimeProvider(Utc(2026, 7, 27)));
        var overCap = await service.CreateAsync(request.Id, request.Items.Single().Id, 101, RefundMethod.ManualBankTransfer, "refund-over-cap", graph.Admin.Id);
        Assert.False(overCap.Success);
        var created = await service.CreateAsync(request.Id, request.Items.Single().Id, 100, RefundMethod.ManualBankTransfer, "refund-key", graph.Admin.Id);
        Assert.True(created.Success);
        var confirmed = await service.ConfirmManualAsync(created.Refund!.Id, "BANK-001", "proof.jpg", graph.Customer.Id);
        Assert.True(confirmed.Success); Assert.Equal(PaymentStatus.Refunded, graph.Order.PaymentStatus); Assert.Equal(OrderStatus.Delivered, graph.Order.Status);
    }

    private static ApplicationDbContext CreateContext() => new(TestDbContextFactory.CreateSqliteOptions());
    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);
    private static ReturnEligibilityService Eligibility(ApplicationDbContext db, TimeProvider clock) => new(db, new ReturnPolicyService(db), clock);
    private static ReturnService Returns(ApplicationDbContext db, TimeProvider clock) => new(db, Eligibility(db, clock), new RefundAmountCalculator(db), clock);
    private static ReturnSubmitViewModel Submit(Graph g, string key, int quantity) => new() { OrderId = g.Order.Id, IdempotencyKey = key, Items = { new ReturnSubmitItemViewModel { Selected = true, OrderItemId = g.Item.Id, Quantity = quantity, Reason = ReturnReasonCode.Other, RequestedResolution = ReturnResolutionType.PartialRefund, Description = "Sản phẩm không đạt chất lượng" } } };
    private static ReturnPolicy Policy(ReturnPolicyScope scope, ReturnReasonCode reason, int hours, DateTime from, int? categoryId = null, int? productId = null, int version = 1) => new() { Name = $"{scope}-{version}-{Guid.NewGuid():N}", Scope = scope, CategoryId = categoryId, ProductId = productId, Reason = reason, ClaimWindowHours = hours, IsEligible = reason != ReturnReasonCode.ChangeOfMind, AllowPartialRefund = true, AllowFullRefund = true, AllowReplacement = true, AllowStoreCredit = true, IsActive = true, Version = version, EffectiveFromUtc = from, CreatedAtUtc = from };
    private static ReturnRequest ApprovedRequest(Graph g, ReturnPolicy p, int quantity, decimal amount) => new() { ReturnNumber = $"RT{Guid.NewGuid():N}"[..20], IdempotencyKey = Guid.NewGuid().ToString("N"), OrderId = g.Order.Id, UserId = g.Customer.Id, Status = ReturnRequestStatus.Approved, SubmittedAtUtc = Utc(2026, 7, 27), ClaimDeadlineAtUtc = Utc(2026, 7, 28), ReviewDueAtUtc = Utc(2026, 7, 28), Items = { new ReturnRequestItem { OrderItemId = g.Item.Id, ReturnPolicy = p, RequestedQuantity = quantity, ApprovedQuantity = quantity, Reason = ReturnReasonCode.Other, RequestedResolution = ReturnResolutionType.PartialRefund, Description = "quality issue", NetPaidAmountSnapshot = amount, RequestedAmount = amount, ApprovedAmount = amount, PolicyVersionSnapshot = 1, ClaimWindowHoursSnapshot = 24, ClaimDeadlineAtUtcSnapshot = Utc(2026, 7, 28) } } };

    private static Graph SeedOrder(ApplicationDbContext db, DateTime? delivered = null, int quantity = 1, decimal itemTotal = 100m)
    {
        var customer = new User { Name = "Customer", Email = $"c{Guid.NewGuid():N}@test.local", Password = "hash", Role = UserRole.Customer };
        var admin = new User { Name = "Admin", Email = $"a{Guid.NewGuid():N}@test.local", Password = "hash", Role = UserRole.Admin };
        var category = new Category { Name = "Fruit", Slug = $"fruit-{Guid.NewGuid():N}" };
        var product = new Product { Category = category, Name = "Apple", Slug = $"apple-{Guid.NewGuid():N}", Price = itemTotal / quantity, StockQuantity = 10 };
        var order = new Order { User = customer, OrderNumber = $"ORD-{Guid.NewGuid():N}", Status = OrderStatus.Delivered, PaymentStatus = PaymentStatus.Paid, DeliveredAtUtc = delivered ?? Utc(2026, 7, 27), Subtotal = itemTotal, Total = itemTotal };
        var item = new OrderItem { Order = order, Product = product, ProductName = product.Name, Quantity = quantity, BasePrice = itemTotal / quantity, Price = itemTotal / quantity, Total = itemTotal };
        order.Items.Add(item); db.AddRange(admin, order); db.SaveChanges(); return new(customer, admin, product, order, item);
    }
    private sealed record Graph(User Customer, User Admin, Product Product, Order Order, OrderItem Item);
    private sealed class MutableTimeProvider(DateTime utcNow) : TimeProvider { public DateTime UtcNow { get; set; } = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc); public override DateTimeOffset GetUtcNow() => new(UtcNow); }
}
