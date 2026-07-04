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
    public async Task CalculateShippingAjax_DerivesPackageSizeFromCartAndPassesGhnCodesToShippingService()
    {
        var shippingService = new Mock<IShippingService>();
        var cartService = new Mock<ICartService>();
        var couponService = new Mock<ICouponService>();

        var expectedPackage = ShippingPackage.FromTotalKg(3);
        var cart = new CartViewModel
        {
            Items =
            {
                new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10000m, Quantity = 3 }
            },
            Subtotal = 30000m
        };

        cartService
            .Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cart);

        shippingService
            .Setup(service => service.CalculateShippingAsync(
                It.Is<decimal>(s => s == 30000m),
                It.Is<string>(d => d == "Phuong Ben Nghe"),
                It.Is<int?>(id => id == 1442),
                It.Is<string?>(w => w == "20101"),
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)))
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
            District = "Phuong Ben Nghe",
            GhnDistrictId = 1442,
            GhnWardCode = "20101"
        });

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);

        shippingService.Verify(service => service.CalculateShippingAsync(
                30000m,
                "Phuong Ben Nghe",
                1442,
                "20101",
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)),
            Times.Once);
    }

    [Fact]
    public async Task CalculateShippingAjax_EmptyCart_PassesZeroSubtotalAndGhnCodesToShippingService()
    {
        var shippingService = new Mock<IShippingService>();
        var cartService = new Mock<ICartService>();
        var couponService = new Mock<ICouponService>();

        var expectedPackage = ShippingPackage.FromTotalKg(0);
        var cart = new CartViewModel
        {
            Items = new List<CartItemViewModel>(),
            Subtotal = 0m
        };

        cartService
            .Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cart);

        shippingService
            .Setup(service => service.CalculateShippingAsync(
                It.Is<decimal>(s => s == 0m),
                It.Is<string>(d => d == "Phuong Ben Nghe"),
                It.Is<int?>(id => id == 1442),
                It.Is<string?>(w => w == "20101"),
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)))
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
            District = "Phuong Ben Nghe",
            GhnDistrictId = 1442,
            GhnWardCode = "20101"
        });

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);

        shippingService.Verify(service => service.CalculateShippingAsync(
                0m,
                "Phuong Ben Nghe",
                1442,
                "20101",
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)),
            Times.Once);
    }

    [Fact]
    public async Task CalculateShippingAjax_MissingGhnCodes_PassesNullCodesToShippingService()
    {
        var shippingService = new Mock<IShippingService>();
        var cartService = new Mock<ICartService>();
        var couponService = new Mock<ICouponService>();

        var expectedPackage = ShippingPackage.FromTotalKg(3);
        var cart = new CartViewModel
        {
            Items =
            {
                new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10000m, Quantity = 3 }
            },
            Subtotal = 30000m
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
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)))
            .ReturnsAsync(new ShippingInfo
            {
                ShippingFee = 0m,
                Zone = ShippingZone.Zone3_Remote,
                Message = "Không thể tính phí vận chuyển GHN"
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
            District = "Phuong Ben Nghe"
        });

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);

        shippingService.Verify(service => service.CalculateShippingAsync(
                30000m,
                "Phuong Ben Nghe",
                null,
                null,
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)),
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
