using Fruitables.Models;

namespace Fruitables.Services.Pricing;

public sealed record ComboPriceResult(
    decimal OriginalTotal,
    decimal FinalTotal,
    decimal Discount);

public static class ComboPricingCalculator
{
    public static ComboPriceResult Calculate(
        ComboPricingType pricingType,
        decimal originalTotal,
        decimal? fixedPrice,
        decimal? discountValue)
    {
        originalTotal = Math.Max(0, decimal.Round(originalTotal, 2));
        var finalTotal = pricingType switch
        {
            ComboPricingType.FixedPrice => fixedPrice ?? originalTotal,
            ComboPricingType.PercentageDiscount =>
                originalTotal * (1 - (discountValue ?? 0) / 100m),
            ComboPricingType.FixedDiscount => originalTotal - (discountValue ?? 0),
            _ => originalTotal
        };

        finalTotal = decimal.Round(Math.Clamp(finalTotal, 0, originalTotal), 2);
        return new ComboPriceResult(
            originalTotal,
            finalTotal,
            decimal.Round(originalTotal - finalTotal, 2));
    }
}
