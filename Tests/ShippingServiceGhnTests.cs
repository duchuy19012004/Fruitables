using Fruitables.Models;
using Fruitables.Services.Communications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Fruitables.Services.Infrastructure;
using Fruitables.Services.Shipping.Delivery;
using Fruitables.Services.Shipping.Providers;

namespace Fruitables.Tests;

public class ShippingServiceGhnTests
{
    [Fact]
    public async Task CalculateShippingAsync_UsesGhnFee_WhenAddressCodesExist()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();

        ghn.Setup(service => service.CalculateFeeAsync(
                1442,
                "20101",
                3000,
                30,
                20,
                15,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(32000m);

        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object);

        var result = await service.CalculateShippingAsync(
            100000m,
            "Phuong Ben Nghe",
            1442,
            "20101",
            ShippingPackage.FromTotalKg(3));

        Assert.Equal(32000m, result.ShippingFee);
        Assert.Equal("Phí vận chuyển GHN", result.Message);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
    }

    [Fact]
    public async Task CalculateShippingAsync_DoesNotCallGhn_WhenPackageMissing()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();
        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object);

        var result = await service.CalculateShippingAsync(100000m, "Phuong Ben Nghe", 1442, "20101");

        Assert.Equal(0m, result.ShippingFee);
        Assert.Equal("Không tính được phí vận chuyển GHN", result.Message);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
        ghn.Verify(service => service.CalculateFeeAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CalculateShippingAsync_FallsBack_WhenGhnReturnsNull()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();

        ghn.Setup(service => service.CalculateFeeAsync(
                1442,
                "20101",
                3000,
                30,
                20,
                15,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object);

        var result = await service.CalculateShippingAsync(
            100000m,
            "Unknown",
            1442,
            "20101",
            ShippingPackage.FromTotalKg(3));

        Assert.Equal(0m, result.ShippingFee);
        Assert.Equal("Không tính được phí vận chuyển GHN", result.Message);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
    }

    private static Mock<ISettingsService> CreateSettingsService()
    {
        var settings = new Mock<ISettingsService>();

        settings.Setup(service => service.GetSettingAsync<decimal?>(It.IsAny<string>(), It.IsAny<decimal?>()))
            .ReturnsAsync((decimal?)null);
        settings.Setup(service => service.GetSettingAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((string?)null);

        return settings;
    }
}
