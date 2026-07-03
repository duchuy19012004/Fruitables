using Fruitables.Controllers;
using Fruitables.Models;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class CartControllerTests
{
    [Fact]
    public async Task CalculateShippingAjax_DerivesPackageSizeFromCartAndPassesToShippingService()
    {
        var shippingService = new Mock<IShippingService>();
        var cartService = new Mock<ICartService>();
        var couponService = new Mock<ICouponService>();

        var packageSize = ShippingPackageCalculator.Calculate(3);
        var cart = new CartViewModel
        {
            Items =
            {
                new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10000m, Quantity = 3 }
            },
            Subtotal = 30000m,
            PackageSize = packageSize
        };

        cartService
            .Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cart);

        shippingService
            .Setup(service => service.CalculateShippingAsync(
                It.Is<decimal>(s => s == 30000m),
                It.Is<string>(d => d == "Phuong Ben Nghe"),
                It.Is<int?>(id => id == null),
                It.Is<string?>(w => w == null),
                It.Is<PackageSize?>(ps => ps != null && ps.WeightGrams == packageSize.WeightGrams)))
            .ReturnsAsync(new ShippingInfo
            {
                ShippingFee = 32000m,
                Zone = ShippingZone.Zone3_Remote,
                Message = "Phi van chuyen GHN"
            });

        var controller = new CartController(
            cartService.Object,
            shippingService.Object,
            couponService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Session = new TestSession()
                }
            }
        };

        var result = await controller.CalculateShippingAjax(new CartController.CalculateShippingRequest
        {
            Subtotal = 30000m,
            District = "Phuong Ben Nghe"
        });

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);

        shippingService.Verify(service => service.CalculateShippingAsync(
                30000m,
                "Phuong Ben Nghe",
                null,
                null,
                It.Is<PackageSize?>(ps => ps != null && ps.WeightGrams == packageSize.WeightGrams)),
            Times.Once);
    }

    [Fact]
    public async Task CalculateShippingAjax_EmptyCart_PreservesZeroSubtotalBehavior()
    {
        var shippingService = new Mock<IShippingService>();
        var cartService = new Mock<ICartService>();
        var couponService = new Mock<ICouponService>();

        var packageSize = ShippingPackageCalculator.Calculate(0);
        var cart = new CartViewModel
        {
            Items = new List<CartItemViewModel>(),
            Subtotal = 0m,
            PackageSize = packageSize
        };

        cartService
            .Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cart);

        shippingService
            .Setup(service => service.CalculateShippingAsync(
                It.Is<decimal>(s => s == 0m),
                It.IsAny<string>(),
                It.Is<int?>(id => id == null),
                It.IsAny<string?>(),
                It.Is<PackageSize?>(ps => ps != null && ps.WeightGrams == 0)))
            .ReturnsAsync(new ShippingInfo
            {
                ShippingFee = 0m,
                Zone = ShippingZone.Zone3_Remote,
                Message = string.Empty
            });

        var controller = new CartController(
            cartService.Object,
            shippingService.Object,
            couponService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Session = new TestSession()
                }
            }
        };

        var result = await controller.CalculateShippingAjax(new CartController.CalculateShippingRequest
        {
            Subtotal = 0m,
            District = "Phuong Ben Nghe"
        });

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);

        shippingService.Verify(service => service.CalculateShippingAsync(
                0m,
                It.IsAny<string>(),
                null,
                It.IsAny<string?>(),
                It.Is<PackageSize?>(ps => ps != null && ps.WeightGrams == 0)),
            Times.Once);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
