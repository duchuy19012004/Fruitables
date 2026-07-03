using Fruitables.Models;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

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
                1000,
                20,
                15,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(32000m);

        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object,
            CreateOptions());

        var result = await service.CalculateShippingAsync(100000m, "Phuong Ben Nghe", 1442, "20101");

        Assert.Equal(32000m, result.ShippingFee);
        Assert.Equal("Phi van chuyen GHN", result.Message);
    }

    [Fact]
    public async Task CalculateShippingAsync_FallsBack_WhenGhnReturnsNull()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();

        ghn.Setup(service => service.CalculateFeeAsync(
                1442,
                "20101",
                1000,
                20,
                15,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object,
            CreateOptions());

        var result = await service.CalculateShippingAsync(100000m, "Unknown", 1442, "20101");

        Assert.Equal(50000m, result.ShippingFee);
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

    private static IOptions<GhnOptions> CreateOptions()
    {
        return Options.Create(new GhnOptions
        {
            DefaultWeight = 1000,
            DefaultLength = 20,
            DefaultWidth = 15,
            DefaultHeight = 10
        });
    }
}
