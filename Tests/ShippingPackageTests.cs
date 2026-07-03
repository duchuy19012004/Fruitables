using Fruitables.Models;
using Xunit;

namespace Fruitables.Tests;

public class ShippingPackageTests
{
    [Theory]
    [InlineData(1, 1000, 20, 15, 10)]
    [InlineData(2, 2000, 20, 15, 10)]
    [InlineData(3, 3000, 30, 20, 15)]
    [InlineData(5, 5000, 30, 20, 15)]
    [InlineData(6, 6000, 40, 30, 20)]
    public void FromTotalKg_UsesFruitBoxTiers(
        int totalKg,
        int expectedWeight,
        int expectedLength,
        int expectedWidth,
        int expectedHeight)
    {
        var package = ShippingPackage.FromTotalKg(totalKg);

        Assert.Equal(expectedWeight, package.Weight);
        Assert.Equal(expectedLength, package.Length);
        Assert.Equal(expectedWidth, package.Width);
        Assert.Equal(expectedHeight, package.Height);
    }

    [Fact]
    public void FromTotalKg_ClampsZeroAndNegativeWeightToZero()
    {
        var package = ShippingPackage.FromTotalKg(0);

        Assert.Equal(0, package.Weight);
        Assert.Equal(20, package.Length);
        Assert.Equal(15, package.Width);
        Assert.Equal(10, package.Height);
    }

    [Fact]
    public void CartViewModel_ShippingPackage_UsesSumOfItemQuantitiesAsKilograms()
    {
        var cart = new Fruitables.ViewModels.CartViewModel
        {
            Items =
            {
                new Fruitables.ViewModels.CartItemViewModel { Quantity = 2 },
                new Fruitables.ViewModels.CartItemViewModel { Quantity = 3 }
            }
        };

        var package = cart.ShippingPackage;

        Assert.Equal(5000, package.Weight);
        Assert.Equal(30, package.Length);
        Assert.Equal(20, package.Width);
        Assert.Equal(15, package.Height);
    }
}
