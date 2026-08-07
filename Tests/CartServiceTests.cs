using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Fruitables.Services.Pricing.Coupons;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.Services.Orders.Cart;

namespace Fruitables.Tests;

public class CartServiceTests
{
    private static readonly VersionedJsonSerializer Serializer = new();

    private static string LinesJson(params CartLineDocument[] lines) =>
        Serializer.Serialize(new CartLinesDocument
        {
            Lines = lines.ToList(),
            NextLineId = lines.Select(line => line.Id).DefaultIfEmpty(0).Max() + 1
        });

    private static IProductPricingService CreateDefaultPricing()
    {
        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(service => service.GetQuoteAsync(
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((int productId, int? variantId, DateTimeOffset? _) =>
                new PriceQuote(productId, variantId, 100_000, 100_000, null));
        pricing.Setup(service => service.GetQuotesAsync(
                It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) =>
                targets.ToDictionary(t => t, t => new PriceQuote(t.ProductId, t.ProductVariantId, 100_000, 100_000, null)));
        return pricing.Object;
    }

    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task FractionalQuantityPersistence_round_trips_decimal_values()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);

        var category = new Category { Id = 1, Name = "Fruit", Slug = "fruit" };
        var product = new Product
        {
            Id = 1,
            CategoryId = category.Id,
            Name = "Apple",
            Slug = "fractional-apple",
            Unit = "kg",
            Price = 100_000m,
            StockQuantity = 2.5m,
            MinOrderQuantity = 0.1m,
            IsActive = true
        };
        var cart = new Cart { Id = 1, SessionId = "fractional-session" };
        var order = new Order
        {
            Id = 1,
            OrderNumber = "ORD-FRACTIONAL",
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Paid,
            Items =
            [
                new OrderItem
                {
                    Product = product,
                    ProductName = product.Name,
                    Quantity = 0.5m,
                    Price = product.Price,
                    BasePrice = product.Price,
                    Total = 50_000m
                }
            ]
        };

        cart.LinesJson = LinesJson(new CartLineDocument
        {
            Id = 1,
            ProductId = product.Id,
            Quantity = 0.5m,
            Price = product.Price
        });
        context.AddRange(category, product, cart, order);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(2.5m, (await context.Products.FindAsync(product.Id))!.StockQuantity);
        var document = Serializer.Deserialize<CartLinesDocument>((await context.Carts.SingleAsync()).LinesJson);
        Assert.Equal(0.5m, document.Lines.Single().Quantity);
        Assert.Equal(0.5m, (await context.OrderItems.SingleAsync())!.Quantity);
    }

    [Theory]
    [InlineData(1, 1000, 20, 15, 10)]
    [InlineData(3, 3000, 30, 20, 15)]
    [InlineData(6, 6000, 40, 30, 20)]
    public async Task GetCartAsync_ComputesPackageSize_FromItemQuantities(
        int quantity,
        int expectedWeight,
        int expectedLength,
        int expectedWidth,
        int expectedHeight)
    {
        var options = CreateInMemoryOptions();
        using var context = new ApplicationDbContext(options);

        var product = new Product
        {
            Id = 1,
            Name = "Apple",
            Slug = "apple",
            Price = 10000,
            StockQuantity = 100,
            MinOrderQuantity = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var cart = new Cart
        {
            SessionId = "test-session",
            LinesJson = LinesJson(new CartLineDocument
            {
                Id = 1,
                ProductId = product.Id,
                Quantity = quantity,
                Price = product.Price
            })
        };
        context.Products.Add(product);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        var couponServiceMock = new Mock<ICouponService>();
        var unitOfWork = new UnitOfWork(context);
        var cartService = new CartService(unitOfWork, couponServiceMock.Object, CreateDefaultPricing());

        var result = await cartService.GetCartAsync("test-session");

        Assert.Equal(expectedWeight, result.ShippingPackage.Weight);
        Assert.Equal(expectedLength, result.ShippingPackage.Length);
        Assert.Equal(expectedWidth, result.ShippingPackage.Width);
        Assert.Equal(expectedHeight, result.ShippingPackage.Height);
        Assert.Null(result.ShippingInfo);
        Assert.Equal(0m, result.ShippingFee);
    }

    [Fact]
    public async Task GetCartAsync_EmptyCart_ProducesZeroWeightPackageSize()
    {
        var options = CreateInMemoryOptions();
        using var context = new ApplicationDbContext(options);

        context.Carts.Add(new Cart { SessionId = "empty-session" });
        await context.SaveChangesAsync();

        var couponServiceMock = new Mock<ICouponService>();
        var unitOfWork = new UnitOfWork(context);
        var cartService = new CartService(unitOfWork, couponServiceMock.Object, CreateDefaultPricing());

        var result = await cartService.GetCartAsync("empty-session");

        Assert.Equal(0, result.ShippingPackage.Weight);
        Assert.Equal(20, result.ShippingPackage.Length);
        Assert.Equal(15, result.ShippingPackage.Width);
        Assert.Equal(10, result.ShippingPackage.Height);
        Assert.Null(result.ShippingInfo);
        Assert.Equal(0m, result.ShippingFee);
    }

    [Fact]
    public async Task GetCartAsync_SumsQuantities_WhenMultipleItems()
    {
        var options = CreateInMemoryOptions();
        using var context = new ApplicationDbContext(options);

        var product1 = new Product
        {
            Id = 1,
            Name = "Apple",
            Slug = "apple",
            Price = 10000,
            StockQuantity = 100,
            MinOrderQuantity = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var product2 = new Product
        {
            Id = 2,
            Name = "Banana",
            Slug = "banana",
            Price = 15000,
            StockQuantity = 100,
            MinOrderQuantity = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var cart = new Cart
        {
            SessionId = "multi-session",
            LinesJson = LinesJson(
                new CartLineDocument { Id = 1, ProductId = product1.Id, Quantity = 2, Price = product1.Price },
                new CartLineDocument { Id = 2, ProductId = product2.Id, Quantity = 3, Price = product2.Price })
        };
        context.Products.AddRange(product1, product2);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        var couponServiceMock = new Mock<ICouponService>();
        var unitOfWork = new UnitOfWork(context);
        var cartService = new CartService(unitOfWork, couponServiceMock.Object, CreateDefaultPricing());

        var result = await cartService.GetCartAsync("multi-session");

        Assert.Equal(5000, result.ShippingPackage.Weight);
        Assert.Equal(30, result.ShippingPackage.Length);
        Assert.Equal(20, result.ShippingPackage.Width);
        Assert.Equal(15, result.ShippingPackage.Height);
        Assert.Null(result.ShippingInfo);
        Assert.Equal(0m, result.ShippingFee);
    }

    [Fact]
    public async Task AddToCartAsync_when_quote_is_missing_adds_nothing_and_returns_failure()
    {
        var options = CreateInMemoryOptions();
        await using var context = new ApplicationDbContext(options);

        context.Products.Add(new Product
        {
            Id = 1,
            Name = "Apple",
            Slug = "apple-missing-quote",
            Price = 50_000,
            StockQuantity = 10,
            MinOrderQuantity = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(service => service.GetQuoteAsync(
                1,
                null,
                It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((PriceQuote?)null);

        var service = new CartService(
            new UnitOfWork(context),
            Mock.Of<ICouponService>(),
            pricing.Object);

        var result = await service.AddToCartAsync(
            "missing-quote-session",
            productId: 1,
            quantity: 1);

        Assert.False(result.Success);
        Assert.Contains("giá", result.Message, StringComparison.OrdinalIgnoreCase);
        var cart = await context.Carts.SingleOrDefaultAsync(item => item.SessionId == "missing-quote-session");
        if (cart != null)
        {
            var document = Serializer.Deserialize<CartLinesDocument>(cart.LinesJson == "[]"
                ? Serializer.Serialize(new CartLinesDocument())
                : cart.LinesJson);
            // empty or default document only
            Assert.Empty(document.Lines);
        }
    }
}
