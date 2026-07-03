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
        var packageSize = ShippingPackageCalculator.Calculate(3);

        ghn.Setup(service => service.CalculateFeeAsync(
                1442,
                "20101",
                packageSize.WeightGrams,
                packageSize.Length,
                packageSize.Width,
                packageSize.Height,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(32000m);

        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object,
            CreateOptions());

        var result = await service.CalculateShippingAsync(
            100000m,
            "Phuong Ben Nghe",
            1442,
            "20101",
            packageSize);

        Assert.Equal(32000m, result.ShippingFee);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
        Assert.Equal("Phi van chuyen GHN", result.Message);
    }

    [Fact]
    public async Task CalculateShippingAsync_ReturnsFailure_WhenGhnReturnsNull()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();
        var packageSize = ShippingPackageCalculator.Calculate(3);

        ghn.Setup(service => service.CalculateFeeAsync(
                1442,
                "20101",
                packageSize.WeightGrams,
                packageSize.Length,
                packageSize.Width,
                packageSize.Height,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object,
            CreateOptions());

        var result = await service.CalculateShippingAsync(
            100000m,
            "Unknown",
            1442,
            "20101",
            packageSize);

        Assert.Equal(0m, result.ShippingFee);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
        Assert.Equal("Không thể tính phí vận chuyển GHN", result.Message);
    }

    [Fact]
    public async Task CalculateShippingAsync_PassesDerivedPackageSize_InsteadOfDefaultGhnOptions()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();
        // Deliberately choose a cart weight that produces dimensions different from
        // the GHN default options (1000g, 20x15x10) to prove derived values are used.
        var packageSize = ShippingPackageCalculator.Calculate(6);

        ghn.Setup(service => service.CalculateFeeAsync(
                1442,
                "20101",
                packageSize.WeightGrams,
                packageSize.Length,
                packageSize.Width,
                packageSize.Height,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(45000m);

        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object,
            CreateOptions());

        var result = await service.CalculateShippingAsync(
            100000m,
            "Phuong Ben Nghe",
            1442,
            "20101",
            packageSize);

        Assert.Equal(45000m, result.ShippingFee);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
        Assert.Equal("Phi van chuyen GHN", result.Message);

        ghn.Verify(service => service.CalculateFeeAsync(
                1442,
                "20101",
                6000,
                40,
                30,
                20,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Sanity check: the values passed to GHN are not the default option values.
        Assert.NotEqual(1000, packageSize.WeightGrams);
        Assert.NotEqual(20, packageSize.Length);
        Assert.NotEqual(15, packageSize.Width);
        Assert.NotEqual(10, packageSize.Height);
    }

    [Fact]
    public async Task CalculateShippingAsync_DoesNotCallGhn_WhenPackageMissing()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();
        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object,
            CreateOptions());

        var result = await service.CalculateShippingAsync(
            100000m,
            "Phuong Ben Nghe",
            1442,
            "20101");

        Assert.Equal(0m, result.ShippingFee);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
        Assert.Equal("Không thể tính phí vận chuyển GHN", result.Message);
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
    public async Task CalculateShippingAsync_ReturnsEmptyMessage_WhenSubtotalIsZero()
    {
        var settings = CreateSettingsService();
        var ghn = new Mock<IGhnService>();
        var packageSize = ShippingPackageCalculator.Calculate(3);
        var service = new ShippingService(
            settings.Object,
            NullLogger<ShippingService>.Instance,
            ghn.Object,
            CreateOptions());

        var result = await service.CalculateShippingAsync(
            0m,
            "Phuong Ben Nghe",
            1442,
            "20101",
            packageSize);

        Assert.Equal(0m, result.ShippingFee);
        Assert.Equal(ShippingZone.Zone3_Remote, result.Zone);
        Assert.Equal(string.Empty, result.Message);
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
