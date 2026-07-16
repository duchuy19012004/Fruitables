using Fruitables.Services.Analytics;
using Fruitables.ViewModels;
using Xunit;

namespace Fruitables.Tests;

public class AnalyticsPeriodHelperTests
{
    [Fact]
    public void ResolvePair_Last30Days_PreviousIsContiguousEqualLength()
    {
        var today = new DateTime(2026, 7, 16);
        var pair = AnalyticsPeriodHelper.ResolvePair(DateRangePreset.Last30Days, null, null, today);

        Assert.Equal(new DateTime(2026, 6, 17), pair.Current.StartInclusive.Date);
        Assert.Equal(new DateTime(2026, 7, 17), pair.Current.EndExclusive.Date);

        var currentDays = (pair.Current.EndExclusive - pair.Current.StartInclusive).TotalDays;
        var prevDays = (pair.Previous.EndExclusive - pair.Previous.StartInclusive).TotalDays;
        Assert.Equal(currentDays, prevDays);
        Assert.Equal(pair.Current.StartInclusive, pair.Previous.EndExclusive);
    }

    [Fact]
    public void ResolvePair_CustomOver366Days_Throws()
    {
        var from = new DateTime(2025, 1, 1);
        var to = new DateTime(2026, 2, 2);
        Assert.Throws<ArgumentException>(() =>
            AnalyticsPeriodHelper.ResolvePair(DateRangePreset.Custom, from, to, new DateTime(2026, 7, 16)));
    }

    [Fact]
    public void MetricValue_From_PreviousZeroCurrentPositive_DeltaPercentNull()
    {
        var m = MetricValue.From(100, 0);
        Assert.Null(m.DeltaPercent);
        Assert.Equal(100, m.Delta);
    }
}
