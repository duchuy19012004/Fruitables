namespace Fruitables.Services.Pricing;

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
}
