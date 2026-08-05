using Fruitables.Services.Orders;
using Xunit;

namespace Fruitables.Tests;

public sealed class QuantityRulesTests
{
    public static TheoryData<string, decimal, decimal, bool> Cases => new()
    {
        { "kg", 0.1m, 0.1m, true },
        { "kg", 0.5m, 0.1m, true },
        { "kg", 0.05m, 0.1m, false },
        { "kg", 0.11m, 0.1m, false },
        { "quả", 1m, 1m, true },
        { "quả", 0.5m, 1m, false }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void IsValid_enforces_unit_precision(
        string unit, decimal quantity, decimal minimum, bool expected)
    {
        Assert.Equal(expected, QuantityRules.IsValid(unit, quantity, minimum));
    }
}
