using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class OrderVariantStockTests
{
    [Fact]
    public async Task CreateOrder_deducts_variant_stock_and_snapshots_variant_identity()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 50_000, StockQuantity = 99 });
        context.ProductVariants.Add(new ProductVariant { Id = 7, ProductId = 1, Name = "Hộp 2kg", SKU = "TAO-2", Price = 180_000, StockQuantity = 5 });
        await context.SaveChangesAsync();

        var cart = new Mock<ICartService>();
        var cartSnapshot = new CartViewModel
        {
            Items = [new CartItemViewModel { ProductId = 1, ProductVariantId = 7, VariantName = "Hộp 2kg", VariantSKU = "TAO-2", ProductName = "Táo", Price = 180_000, Quantity = 2 }],
            Subtotal = 360_000, Total = 360_000
        };
        cart.Setup(c => c.GetCartAsync("s", It.IsAny<string?>())).ReturnsAsync(cartSnapshot);
        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(p => p.GetQuotesAsync(It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync(new Dictionary<PriceTargetKey, PriceQuote>
            {
                [new PriceTargetKey(1, 7)] = new PriceQuote(1, 7, 180_000, 180_000, null)
            });
        var service = new OrderService(new UnitOfWork(context), cart.Object, Mock.Of<IRealtimeNotifier>(), pricing.Object, Mock.Of<ILogger<OrderService>>());

        var order = await service.CreateOrderAsync(new CheckoutViewModel { PaymentMethod = PaymentMethod.COD }, "s");

        Assert.Equal(3, context.ProductVariants.Find(7)!.StockQuantity);
        Assert.Equal(99, context.Products.Find(1)!.StockQuantity);
        var item = Assert.Single(order.Items);
        Assert.Equal(7, item.ProductVariantId);
        Assert.Equal("TAO-2", item.VariantSKU);
    }

    [Fact]
    public async Task CancelOrder_restores_stock_to_the_original_variant_only()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        var product = new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 50_000, StockQuantity = 99 };
        var variant = new ProductVariant { Id = 7, ProductId = 1, Name = "Hộp 2kg", SKU = "TAO-2", Price = 180_000, StockQuantity = 3 };
        var order = new Order { Id = 8, OrderNumber = "ORD-8", Status = OrderStatus.Pending };
        order.Items.Add(new OrderItem
        {
            ProductId = 1, ProductVariantId = 7, ProductName = "Táo", VariantName = "Hộp 2kg",
            VariantSKU = "TAO-2", Quantity = 2, Price = 180_000, Total = 360_000
        });
        context.AddRange(product, variant, order);
        await context.SaveChangesAsync();

        var result = await new OrderRepository(context).CancelOrderWithStockRestoreAsync(8, "Đổi ý");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, context.ProductVariants.Find(7)!.StockQuantity);
        Assert.Equal(99, context.Products.Find(1)!.StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_rejects_a_missing_or_stale_pricing_token()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        var cart = new Mock<ICartService>();
        cart.Setup(service => service.RepriceForCheckoutAsync("s")).ReturnsAsync(new CartViewModel
        {
            PricingToken = "NEW-TOKEN",
            Items = []
        });
        var service = new OrderService(new UnitOfWork(context), cart.Object, Mock.Of<IRealtimeNotifier>(), Mock.Of<IProductPricingService>(), Mock.Of<ILogger<OrderService>>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(new CheckoutViewModel { PricingToken = "OLD-TOKEN" }, "s"));

        Assert.Contains("thay đổi", error.Message);
    }
}
