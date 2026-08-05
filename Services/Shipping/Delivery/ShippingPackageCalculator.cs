using Fruitables.Models;

namespace Fruitables.Services.Shipping.Delivery;

/// <summary>
/// Tính toán kích thước gói hàng dựa trên tổng số kilogram trong giỏ hàng.
/// </summary>
public static class ShippingPackageCalculator
{
    // Các kích thước thùng theo yêu cầu (cm).
    private const int SmallBoxLength = 20;
    private const int SmallBoxWidth = 15;
    private const int SmallBoxHeight = 10;

    private const int MediumBoxLength = 30;
    private const int MediumBoxWidth = 20;
    private const int MediumBoxHeight = 15;

    private const int LargeBoxLength = 40;
    private const int LargeBoxWidth = 30;
    private const int LargeBoxHeight = 20;

    private const decimal SmallBoxMaxKg = 2m;
    private const decimal MediumBoxMaxKg = 5m;
    private const int GramsPerKg = 1000;

    /// <summary>
    /// Tính toán <see cref="PackageSize"/> từ tổng số kilogram.
    /// </summary>
    /// <param name="totalKg">Tổng khối lượng giỏ hàng (kg).</param>
    /// <returns>Kích thước gói hàng phù hợp.</returns>
    public static PackageSize Calculate(decimal totalKg)
    {
        if (totalKg <= 0)
        {
            return new PackageSize(0, SmallBoxLength, SmallBoxWidth, SmallBoxHeight);
        }

        var weightGrams = (int)(totalKg * GramsPerKg);

        if (totalKg <= SmallBoxMaxKg)
        {
            return new PackageSize(weightGrams, SmallBoxLength, SmallBoxWidth, SmallBoxHeight);
        }

        if (totalKg <= MediumBoxMaxKg)
        {
            return new PackageSize(weightGrams, MediumBoxLength, MediumBoxWidth, MediumBoxHeight);
        }

        return new PackageSize(weightGrams, LargeBoxLength, LargeBoxWidth, LargeBoxHeight);
    }
}
