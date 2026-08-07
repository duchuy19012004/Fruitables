namespace Fruitables.Services.Pricing.ProductPricing;

public static class VndPriceRules
{
    public const decimal MaximumPrice = 99_999_999m;

    public static bool IsWholeAmount(decimal value) =>
        value == decimal.Truncate(value);

    public static bool IsValidPrice(decimal value) =>
        value > 0 &&
        value <= MaximumPrice &&
        IsWholeAmount(value);

    public static bool IsValidFixedAdjustment(decimal value) =>
        value > 0 &&
        IsWholeAmount(value);

    public static bool IsValidPercentage(decimal value) =>
        value >= 1 &&
        value <= 99;

    public static decimal CalculatePercentagePrice(decimal basePrice, decimal percentage) =>
        Math.Round(
            basePrice * (100m - percentage) / 100m,
            0,
            MidpointRounding.AwayFromZero);
}
