namespace Fruitables.Services.Orders;

public static class QuantityRules
{
    public static bool IsValid(string? unit, decimal quantity, decimal minimumQuantity)
    {
        if (quantity <= 0 || quantity < minimumQuantity)
            return false;

        var step = string.Equals(unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)
            ? 0.1m
            : 1m;
        var steps = quantity / step;

        return decimal.Truncate(steps) == steps && decimal.Round(steps, 0) == steps;
    }
}
