using Fruitables.Controllers.Api;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Infrastructure;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Orders.Cart;
using Fruitables.Services.Orders.OrderManagement;
using Fruitables.Services.Pricing.Coupons;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public sealed class CommerceAggregateJsonTests
{
    private static readonly VersionedJsonSerializer Serializer = new();

    [Fact]
    public async Task Cart_round_trips_product_and_combo_lines_in_lines_json()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
        context.Products.AddRange(
            new Product { Id = 1, CategoryId = 1, Name = "A", Slug = "a", Price = 100, StockQuantity = 20, IsActive = true },
            new Product { Id = 2, CategoryId = 1, Name = "B", Slug = "b", Price = 100, StockQuantity = 20, IsActive = true });
        context.Combos.Add(new Combo
        {
            Id = 9,
            Name = "Pair",
            Slug = "pair",
            IsActive = true,
            Revision = 1,
            PricingType = ComboPricingType.PercentageDiscount,
            DiscountValue = 10,
            Items =
            [
                new ComboItem { ProductId = 1, Quantity = 1, SortOrder = 0 },
                new ComboItem { ProductId = 2, Quantity = 1, SortOrder = 1 }
            ]
        });
        await context.SaveChangesAsync();

        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(service => service.GetQuoteAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((int productId, int? variantId, DateTimeOffset? _) =>
                new PriceQuote(productId, variantId, 100, 100, null));
        pricing.Setup(service => service.GetQuotesAsync(It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) =>
                targets.ToDictionary(t => t, t => new PriceQuote(t.ProductId, t.ProductVariantId, 100, 100, null)));

        var cartService = new CartService(new UnitOfWork(context), Mock.Of<ICouponService>(), pricing.Object, Serializer);
        Assert.True((await cartService.AddToCartAsync("json-cart", 1, 2)).Success);
        Assert.True((await cartService.AddComboToCartAsync("json-cart", 9)).Success);

        context.ChangeTracker.Clear();
        var stored = await context.Carts.SingleAsync();
        var document = Serializer.Deserialize<CartLinesDocument>(stored.LinesJson);
        Assert.Contains(document.Lines, line => line.CartGroupId == null && line.ProductId == 1 && line.Quantity == 2);
        Assert.Contains(document.Lines, line => line.CartGroupId != null && line.ComboId == 9);
        Assert.Empty(await context.CartItems.ToListAsync());
        Assert.Empty(await context.CartGroups.ToListAsync());
    }

    [Fact]
    public async Task Order_note_and_status_history_append_to_order_json()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User { Id = 5, Name = "Admin", Email = "a@example.com", Password = "x", Role = UserRole.Admin, IsActive = true });
        context.Orders.Add(new Order
        {
            Id = 3,
            OrderNumber = "ORD-JSON",
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            Total = 10
        });
        await context.SaveChangesAsync();

        var admin = new OrderAdminService(context, Mock.Of<IOrderLogService>(), Mock.Of<IRealtimeNotifier>(), Serializer);
        await admin.AddOrderNoteAsync(3, "check packaging", 5, "Admin");
        var status = await admin.UpdateOrderStatusAsync(new ViewModels.UpdateOrderStatusRequest
        {
            OrderId = 3,
            NewStatus = OrderStatus.Processing,
            AdminId = 5,
            Notes = "start packing"
        });
        Assert.True(status.Success, status.ErrorMessage);

        context.ChangeTracker.Clear();
        var order = await context.Orders.SingleAsync();
        var notes = Serializer.Deserialize<OrderNotesDocument>(order.NotesJson);
        var history = Serializer.Deserialize<OrderStatusHistoryDocument>(order.StatusHistoryJson);
        Assert.Single(notes.Notes);
        Assert.Equal("check packaging", notes.Notes[0].Content);
        Assert.Single(history.Entries);
        Assert.Equal(OrderStatus.Processing, history.Entries[0].NewStatus);
        Assert.Empty(await context.OrderNotes.ToListAsync());
        Assert.Empty(await context.OrderStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task SePay_webhook_writes_payment_and_is_idempotent()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);
        context.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-PAY",
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentStatus = PaymentStatus.Pending,
            PaymentCode = "FTB123",
            Total = 1000
        });
        await context.SaveChangesAsync();

        const string body = """{"id":42,"code":"FTB123","transferType":"in","transferAmount":1000,"referenceCode":"R1"}""";
        var first = await CreateSePay(context, body).Receive();
        var second = await CreateSePay(context, body).Receive();
        Assert.IsType<OkObjectResult>(first);
        Assert.IsType<OkObjectResult>(second);

        Assert.Equal(1, await context.Payments.CountAsync(payment => payment.ProviderTransactionId == "42"));
        Assert.Equal(PaymentStatus.Paid, (await context.Orders.SingleAsync()).PaymentStatus);
        Assert.Empty(await context.SePayTransactions.ToListAsync());
    }

    private static SePayWebhookController CreateSePay(ApplicationDbContext context, string body, string secret = "test-secret")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SePayOptions
        {
            WebhookSecret = secret,
            PaymentCodePrefix = "FTB"
        });
        var controller = new SePayWebhookController(context, options, NullLogger<SePayWebhookController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        httpContext.Request.Headers["X-SePay-Timestamp"] = timestamp;
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(timestamp + "." + body));
        httpContext.Request.Headers["X-SePay-Signature"] = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
