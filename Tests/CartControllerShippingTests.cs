using Fruitables.Controllers;
using Fruitables.Models;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class CartControllerShippingTests
{
    [Fact]
    public async Task CalculateShippingAjax_PassesGhnCodesAndCartPackageToShippingService()
    {
        var shippingService = new Mock<IShippingService>();
        shippingService
            .Setup(service => service.CalculateShippingAsync(
                417000m,
                "Phuong An Hai",
                1528,
                "910363",
                ShippingPackage.FromTotalKg(3)))
            .ReturnsAsync(new ShippingInfo
            {
                ShippingFee = 53900m,
                Zone = ShippingZone.Zone3_Remote,
                Message = "Phi van chuyen GHN"
            });

        var cartService = new Mock<ICartService>();
        cartService
            .Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new CartViewModel
            {
                Items =
                {
                    new CartItemViewModel { Quantity = 3 }
                },
                Subtotal = 417000m
            });

        var controller = new CartController(
            cartService.Object,
            shippingService.Object,
            Mock.Of<ICouponService>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Session = new TestSession()
            }
        };

        await controller.CalculateShippingAjax(new CartController.CalculateShippingRequest
        {
            District = "Phuong An Hai",
            GhnDistrictId = 1528,
            GhnWardCode = "910363"
        });

        shippingService.Verify(
            service => service.CalculateShippingAsync(
                417000m,
                "Phuong An Hai",
                1528,
                "910363",
                ShippingPackage.FromTotalKg(3)),
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
