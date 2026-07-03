using Fruitables.Models;
using Fruitables.Services;
using Xunit;

namespace Fruitables.Tests;

public class ShippingPackageCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 20, 15, 10)]
    [InlineData(-1, 0, 20, 15, 10)]
    public void Calculate_ReturnsDefaultSmallBox_WhenTotalKgIsZeroOrNegative(
        int totalKg,
        int expectedWeight,
        int expectedLength,
        int expectedWidth,
        int expectedHeight)
    {
        var result = ShippingPackageCalculator.Calculate(totalKg);

        Assert.Equal(expectedWeight, result.WeightGrams);
        Assert.Equal(expectedLength, result.Length);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Theory]
    [InlineData(1, 1000, 20, 15, 10)]
    [InlineData(2, 2000, 20, 15, 10)]
    public void Calculate_ReturnsSmallBox_WhenTotalKgIsTwoOrLess(
        int totalKg,
        int expectedWeight,
        int expectedLength,
        int expectedWidth,
        int expectedHeight)
    {
        var result = ShippingPackageCalculator.Calculate(totalKg);

        Assert.Equal(expectedWeight, result.WeightGrams);
        Assert.Equal(expectedLength, result.Length);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Theory]
    [InlineData(3, 3000, 30, 20, 15)]
    [InlineData(4, 4000, 30, 20, 15)]
    [InlineData(5, 5000, 30, 20, 15)]
    public void Calculate_ReturnsMediumBox_WhenTotalKgIsBetweenTwoAndFive(
        int totalKg,
        int expectedWeight,
        int expectedLength,
        int expectedWidth,
        int expectedHeight)
    {
        var result = ShippingPackageCalculator.Calculate(totalKg);

        Assert.Equal(expectedWeight, result.WeightGrams);
        Assert.Equal(expectedLength, result.Length);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Theory]
    [InlineData(6, 6000, 40, 30, 20)]
    [InlineData(10, 10000, 40, 30, 20)]
    public void Calculate_ReturnsLargeBox_WhenTotalKgIsGreaterThanFive(
        int totalKg,
        int expectedWeight,
        int expectedLength,
        int expectedWidth,
        int expectedHeight)
    {
        var result = ShippingPackageCalculator.Calculate(totalKg);

        Assert.Equal(expectedWeight, result.WeightGrams);
        Assert.Equal(expectedLength, result.Length);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Fact]
    public void Calculate_DoesNotUseProductWeight()
    {
        // The calculator derives weight only from total kilograms.
        // There is no Product.Weight dependency to assert; this test documents
        // that the public API accepts only a kg total and returns grams.
        var result = ShippingPackageCalculator.Calculate(3);

        Assert.Equal(3000, result.WeightGrams);
    }
}
