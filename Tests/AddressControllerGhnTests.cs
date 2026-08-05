using System.Security.Claims;
using Fruitables.Controllers;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;
using Fruitables.Services.Identity.Profiles;
using Fruitables.Services.Shipping.Address;
using Fruitables.Services.Shipping.Providers;

namespace Fruitables.Tests;

public class AddressControllerGhnTests
{
    [Fact]
    public async Task Create_ResolvesGhnCodesBeforeSavingAddress()
    {
        var addressService = new Mock<IAddressService>();
        var profileService = new Mock<IProfileService>();
        var vietnamAddressService = new Mock<IVietnamAddressService>();
        var ghnService = new Mock<IGhnService>();

        vietnamAddressService
            .Setup(service => service.SanitizeStreetAddress("123 Le Loi"))
            .Returns("123 Le Loi");

        ghnService
            .Setup(service => service.ResolveAddressAsync(
                "TP Ho Chi Minh",
                "Phuong Ben Nghe",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GhnAddressCode(1442, "20101"));

        addressService
            .Setup(service => service.CreateAddressAsync(It.IsAny<Address>()))
            .ReturnsAsync((Address address) =>
            {
                address.Id = 10;
                return address;
            });

        var controller = new AddressController(
            addressService.Object,
            profileService.Object,
            vietnamAddressService.Object,
            ghnService.Object)
        {
            ControllerContext = TestControllerContext.WithUserId(1),
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Create(new Address
        {
            FullName = "Nguyen Van A",
            Phone = "0901234567",
            ProvinceCode = "79",
            ProvinceName = "TP Ho Chi Minh",
            CommuneCode = "26734",
            CommuneName = "Phuong Ben Nghe",
            StreetAddress = "123 Le Loi"
        });

        Assert.IsType<RedirectToActionResult>(result);
        addressService.Verify(service => service.CreateAddressAsync(
            It.Is<Address>(address =>
                address.GhnDistrictId == 1442 &&
                address.GhnWardCode == "20101")),
            Times.Once);
    }
}

internal static class TestControllerContext
{
    public static ControllerContext WithUserId(int userId)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "Test"));

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }
}
