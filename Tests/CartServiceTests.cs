using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class CartServiceTests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Theory]
    [InlineData(1, 1000, 20, 15, 10)]
    [InlineData(3, 3000, 30, 20, 15)]
    [InlineData(6, 6000, 40, 30, 20)]
    public async Task GetCartAsync_ComputesPackageSize_FromItemQuantities(
        int quantity,
        int expectedWeightGrams,
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
        var cart = new Cart { SessionId = "test-session" };
        context.Products.Add(product);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        context.CartItems.Add(new CartItem
        {
            CartId = cart.Id,
            ProductId = product.Id,
            Quantity = quantity,
            Price = product.Price
        });
        await context.SaveChangesAsync();

        var shippingServiceMock = new Mock<IShippingService>();
        shippingServiceMock
            .Setup(service => service.CalculateShippingAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<PackageSize?>()))
            .ReturnsAsync(new ShippingInfo { ShippingFee = 0m });

        var couponServiceMock = new Mock<ICouponService>();
        var unitOfWork = new UnitOfWork(context);
        var cartService = new CartService(unitOfWork, shippingServiceMock.Object, couponServiceMock.Object);

        var result = await cartService.GetCartAsync("test-session");

        Assert.NotNull(result.PackageSize);
        Assert.Equal(expectedWeightGrams, result.PackageSize.WeightGrams);
        Assert.Equal(expectedLength, result.PackageSize.Length);
        Assert.Equal(expectedWidth, result.PackageSize.Width);
        Assert.Equal(expectedHeight, result.PackageSize.Height);

        shippingServiceMock.Verify(service => service.CalculateShippingAsync(
            It.IsAny<decimal>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.Is<PackageSize?>(packageSize =>
                packageSize != null &&
                packageSize.WeightGrams == expectedWeightGrams)),
            Times.Once);
    }

    [Fact]
    public async Task GetCartAsync_EmptyCart_ProducesZeroWeightPackageSize()
    {
        var options = CreateInMemoryOptions();
        using var context = new ApplicationDbContext(options);

        context.Carts.Add(new Cart { SessionId = "empty-session" });
        await context.SaveChangesAsync();

        var shippingServiceMock = new Mock<IShippingService>();
        shippingServiceMock
            .Setup(service => service.CalculateShippingAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<PackageSize?>()))
            .ReturnsAsync(new ShippingInfo { ShippingFee = 0m });

        var couponServiceMock = new Mock<ICouponService>();
        var unitOfWork = new UnitOfWork(context);
        var cartService = new CartService(unitOfWork, shippingServiceMock.Object, couponServiceMock.Object);

        var result = await cartService.GetCartAsync("empty-session");

        Assert.NotNull(result.PackageSize);
        Assert.Equal(0, result.PackageSize.WeightGrams);
        Assert.Equal(20, result.PackageSize.Length);
        Assert.Equal(15, result.PackageSize.Width);
        Assert.Equal(10, result.PackageSize.Height);
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
        var cart = new Cart { SessionId = "multi-session" };
        context.Products.AddRange(product1, product2);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        context.CartItems.AddRange(
            new CartItem { CartId = cart.Id, ProductId = product1.Id, Quantity = 2, Price = product1.Price },
            new CartItem { CartId = cart.Id, ProductId = product2.Id, Quantity = 3, Price = product2.Price });
        await context.SaveChangesAsync();

        var shippingServiceMock = new Mock<IShippingService>();
        shippingServiceMock
            .Setup(service => service.CalculateShippingAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<PackageSize?>()))
            .ReturnsAsync(new ShippingInfo { ShippingFee = 0m });

        var couponServiceMock = new Mock<ICouponService>();
        var unitOfWork = new UnitOfWork(context);
        var cartService = new CartService(unitOfWork, shippingServiceMock.Object, couponServiceMock.Object);

        var result = await cartService.GetCartAsync("multi-session");

        Assert.Equal(5000, result.PackageSize?.WeightGrams);
        shippingServiceMock.Verify(service => service.CalculateShippingAsync(
            It.IsAny<decimal>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.Is<PackageSize?>(packageSize => packageSize != null && packageSize.WeightGrams == 5000)),
            Times.Once);
    }
}
