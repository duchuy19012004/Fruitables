using System.Security.Claims;
using Fruitables.Controllers;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class CheckoutControllerTests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task Index_PassesCartPackageSize_ToShippingService()
    {
        var cartService = new Mock<ICartService>();
        var orderService = new Mock<IOrderService>();
        var addressService = new Mock<IAddressService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var vietnamAddressService = new Mock<IVietnamAddressService>();
        var shippingService = new Mock<IShippingService>();
        var logger = new Mock<ILogger<CheckoutController>>();

        var expectedPackage = ShippingPackage.FromTotalKg(3);
        var cart = new CartViewModel
        {
            Items = new List<CartItemViewModel>
            {
                new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10000, Quantity = 3 }
            },
            Subtotal = 30000
        };

        cartService.Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cart);

        addressService.Setup(service => service.GetUserAddressesAsync(1))
            .ReturnsAsync(new List<Address>
            {
                new Address
                {
                    Id = 1,
                    FullName = "Nguyen Van A",
                    Phone = "0901234567",
                    ProvinceCode = "79",
                    ProvinceName = "TP Ho Chi Minh",
                    CommuneCode = "26734",
                    CommuneName = "Phuong Ben Nghe",
                    StreetAddress = "123 Le Loi",
                    IsDefault = true,
                    GhnDistrictId = 1442,
                    GhnWardCode = "20101"
                }
            });

        shippingService.Setup(service => service.CalculateShippingAsync(
                It.Is<decimal>(s => s == 30000m),
                It.Is<string>(d => d == "Phuong Ben Nghe"),
                It.Is<int?>(id => id == 1442),
                It.Is<string?>(w => w == "20101"),
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)))
            .ReturnsAsync(new ShippingInfo { ShippingFee = 32000m, Zone = ShippingZone.Zone3_Remote });

        var httpContext = TestControllerContext.WithUserId(1).HttpContext;
        httpContext.Session = new TestSession();
        httpContext.Session.SetString("SessionId", "session-1");

        var controller = new CheckoutController(
            cartService.Object,
            orderService.Object,
            addressService.Object,
            unitOfWork.Object,
            vietnamAddressService.Object,
            shippingService.Object,
            logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        shippingService.Verify(service => service.CalculateShippingAsync(
                30000m,
                "Phuong Ben Nghe",
                1442,
                "20101",
                expectedPackage),
            Times.Once);
    }

    [Fact]
    public async Task PlaceOrder_PassesCartPackageSize_ToShippingService()
    {
        var options = CreateInMemoryOptions();
        using var context = new ApplicationDbContext(options);

        context.Addresses.Add(new Address
        {
            Id = 1,
            UserId = 1,
            FullName = "Nguyen Van A",
            Phone = "0901234567",
            ProvinceCode = "79",
            ProvinceName = "TP Ho Chi Minh",
            CommuneCode = "26734",
            CommuneName = "Phuong Ben Nghe",
            StreetAddress = "123 Le Loi",
            IsDefault = true,
            GhnDistrictId = 1442,
            GhnWardCode = "20101"
        });
        await context.SaveChangesAsync();

        var cartService = new Mock<ICartService>();
        var orderService = new Mock<IOrderService>();
        var addressService = new Mock<IAddressService>();
        var unitOfWork = new UnitOfWork(context);
        var vietnamAddressService = new Mock<IVietnamAddressService>();
        var shippingService = new Mock<IShippingService>();
        var logger = new Mock<ILogger<CheckoutController>>();

        var expectedPackage = ShippingPackage.FromTotalKg(4);
        var cart = new CartViewModel
        {
            Items = new List<CartItemViewModel>
            {
                new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10000, Quantity = 4 }
            },
            Subtotal = 40000
        };

        cartService.Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cart);

        addressService.Setup(service => service.GetUserAddressesAsync(1))
            .ReturnsAsync(new List<Address>());

        shippingService.Setup(service => service.CalculateShippingAsync(
                It.Is<decimal>(s => s == 40000m),
                It.Is<string>(d => d == "Phuong Ben Nghe"),
                It.Is<int?>(id => id == 1442),
                It.Is<string?>(w => w == "20101"),
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)))
            .ReturnsAsync(new ShippingInfo { ShippingFee = 32000m, Zone = ShippingZone.Zone3_Remote });

        orderService.Setup(service => service.CreateOrderAsync(It.IsAny<CheckoutViewModel>(), "session-2", 1))
            .ReturnsAsync(new Order { OrderNumber = "ORD-001" });

        var httpContext = TestControllerContext.WithUserId(1).HttpContext;
        httpContext.Session = new TestSession();
        httpContext.Session.SetString("SessionId", "session-2");

        var controller = new CheckoutController(
            cartService.Object,
            orderService.Object,
            addressService.Object,
            unitOfWork,
            vietnamAddressService.Object,
            shippingService.Object,
            logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new CheckoutViewModel
        {
            SelectedAddressId = 1,
            PaymentMethod = PaymentMethod.COD
        };

        var result = await controller.PlaceOrder(model);

        Assert.IsType<RedirectToActionResult>(result);
        shippingService.Verify(service => service.CalculateShippingAsync(
                40000m,
                "Phuong Ben Nghe",
                1442,
                "20101",
                expectedPackage),
            Times.Once);
    }

    [Fact]
    public async Task PlaceOrder_WhenCreateOrderThrows_ReloadsSavedAddresses_WithGhnCodes()
    {
        var options = CreateInMemoryOptions();
        using var context = new ApplicationDbContext(options);

        context.Addresses.Add(new Address
        {
            Id = 1,
            UserId = 1,
            FullName = "Nguyen Van A",
            Phone = "0901234567",
            ProvinceCode = "79",
            ProvinceName = "TP Ho Chi Minh",
            CommuneCode = "26734",
            CommuneName = "Phuong Ben Nghe",
            StreetAddress = "123 Le Loi",
            IsDefault = true,
            GhnDistrictId = 1442,
            GhnWardCode = "20101"
        });
        await context.SaveChangesAsync();

        var cartService = new Mock<ICartService>();
        var orderService = new Mock<IOrderService>();
        var addressService = new Mock<IAddressService>();
        var unitOfWork = new UnitOfWork(context);
        var vietnamAddressService = new Mock<IVietnamAddressService>();
        var shippingService = new Mock<IShippingService>();
        var logger = new Mock<ILogger<CheckoutController>>();

        var expectedPackage = ShippingPackage.FromTotalKg(4);
        var cart = new CartViewModel
        {
            Items = new List<CartItemViewModel>
            {
                new CartItemViewModel { ProductId = 1, ProductName = "Apple", Price = 10000, Quantity = 4 }
            },
            Subtotal = 40000
        };

        cartService.Setup(service => service.GetCartAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cart);

        addressService.Setup(service => service.GetUserAddressesAsync(1))
            .ReturnsAsync(new List<Address>
            {
                new Address
                {
                    Id = 1,
                    UserId = 1,
                    FullName = "Nguyen Van A",
                    Phone = "0901234567",
                    ProvinceCode = "79",
                    ProvinceName = "TP Ho Chi Minh",
                    CommuneCode = "26734",
                    CommuneName = "Phuong Ben Nghe",
                    StreetAddress = "123 Le Loi",
                    IsDefault = true,
                    GhnDistrictId = 1442,
                    GhnWardCode = "20101"
                }
            });

        shippingService.Setup(service => service.CalculateShippingAsync(
                It.Is<decimal>(s => s == 40000m),
                It.Is<string>(d => d == "Phuong Ben Nghe"),
                It.Is<int?>(id => id == 1442),
                It.Is<string?>(w => w == "20101"),
                It.Is<ShippingPackage?>(ps => ps != null && ps.Weight == expectedPackage.Weight)))
            .ReturnsAsync(new ShippingInfo { ShippingFee = 32000m, Zone = ShippingZone.Zone3_Remote });

        orderService.Setup(service => service.CreateOrderAsync(It.IsAny<CheckoutViewModel>(), "session-3", 1))
            .ThrowsAsync(new InvalidOperationException("Insufficient inventory"));

        var httpContext = TestControllerContext.WithUserId(1).HttpContext;
        httpContext.Session = new TestSession();
        httpContext.Session.SetString("SessionId", "session-3");

        var controller = new CheckoutController(
            cartService.Object,
            orderService.Object,
            addressService.Object,
            unitOfWork,
            vietnamAddressService.Object,
            shippingService.Object,
            logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var model = new CheckoutViewModel
        {
            SelectedAddressId = 1,
            PaymentMethod = PaymentMethod.COD
        };

        var result = await controller.PlaceOrder(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        var savedAddresses = Assert.IsAssignableFrom<IList<AddressViewModel>>(controller.ViewBag.SavedAddresses);
        var reloadedAddress = Assert.Single(savedAddresses);
        Assert.Equal(1442, reloadedAddress.GhnDistrictId);
        Assert.Equal("20101", reloadedAddress.GhnWardCode);
        Assert.Equal("Phuong Ben Nghe", reloadedAddress.CommuneName);
    }
}

internal class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _storage = new();

    public string Id => "test-session-id";
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _storage.Keys;

    public void Clear() => _storage.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => _storage.Remove(key);

    public void Set(string key, byte[] value) => _storage[key] = value;

    public bool TryGetValue(string key, out byte[] value) => _storage.TryGetValue(key, out value!);
}
