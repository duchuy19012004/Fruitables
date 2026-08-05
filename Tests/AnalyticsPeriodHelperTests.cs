using Fruitables.Services.Analytics.Common;
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
    public void ResolvePair_AllTime_LongHistory_ClampsToLast366Days_DoesNotThrow()
    {
        var today = new DateTime(2026, 7, 16);
        var firstOrder = new DateTime(2020, 1, 1);

        var pair = AnalyticsPeriodHelper.ResolvePair(
            DateRangePreset.AllTime, null, null, today, firstOrder);

        var days = (pair.Current.EndExclusive - pair.Current.StartInclusive).TotalDays;
        Assert.Equal(AnalyticsPeriodHelper.MaxRangeDays, days);
        Assert.Equal(today.AddDays(1), pair.Current.EndExclusive.Date);
        Assert.Equal(today.AddDays(1).AddDays(-AnalyticsPeriodHelper.MaxRangeDays), pair.Current.StartInclusive.Date);
    }

    [Fact]
    public void ResolvePair_AllTime_NoFirstOrder_ClampsToLast366Days()
    {
        var today = new DateTime(2026, 7, 16);

        var pair = AnalyticsPeriodHelper.ResolvePair(
            DateRangePreset.AllTime, null, null, today, firstOrderDate: null);

        var days = (pair.Current.EndExclusive - pair.Current.StartInclusive).TotalDays;
        Assert.Equal(AnalyticsPeriodHelper.MaxRangeDays, days);
        Assert.Equal(today.AddDays(1), pair.Current.EndExclusive.Date);
    }

    [Fact]
    public void MetricValue_From_PreviousZeroCurrentPositive_DeltaPercentNull()
    {
        var m = MetricValue.From(100, 0);
        Assert.Null(m.DeltaPercent);
        Assert.Equal(100, m.Delta);
    }
}
