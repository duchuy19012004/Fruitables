using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Pricing;

public static class PriceCalculator
{
    public static PriceSchedule? SelectApplicableSchedule(
        IEnumerable<PriceSchedule> schedules,
        DateTimeOffset instant)
    {
        return schedules
            .Where(schedule => schedule.IsActiveAt(instant))
            .OrderByDescending(schedule => schedule.StartsAt)
            .ThenByDescending(schedule => schedule.Id)
            .FirstOrDefault();
    }

    public static PriceQuote CalculateQuote(
        decimal basePrice,
        IEnumerable<PriceSchedule> schedules,
        DateTimeOffset instant)
    {
        var active = SelectApplicableSchedule(schedules, instant);
        if (active == null)
            return new PriceQuote(0, null, basePrice, basePrice, null);

        var effectivePrice = active.DiscountType switch
        {
            DiscountType.FixedPrice => active.Value,
            DiscountType.Percentage => Math.Round(
                basePrice * (100m - active.Value) / 100m,
                0,
                MidpointRounding.AwayFromZero),
            _ => basePrice
        };

        return new PriceQuote(0, null, basePrice, effectivePrice, active.Id);
    }
}
