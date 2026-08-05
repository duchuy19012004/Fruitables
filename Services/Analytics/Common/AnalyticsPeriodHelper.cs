using Fruitables.ViewModels;

namespace Fruitables.Services.Analytics.Common;

public static class AnalyticsPeriodHelper
{
    public const int MaxRangeDays = 366;

    /// <summary>
    /// Builds current+previous ranges. Inclusive calendar semantics from DateRangePreset
    /// are converted to [start, endExclusive).
    /// </summary>
    public static AnalyticsPeriodPair ResolvePair(
        DateRangePreset preset,
        DateTime? from,
        DateTime? to,
        DateTime? vietnamToday = null,
        DateTime? firstOrderDate = null)
    {
        var today = (vietnamToday ?? DateRangePresetExtensions.GetVietnamToday()).Date;
        DateTime start;
        DateTime endExclusive;

        if (preset == DateRangePreset.Custom)
        {
            if (!from.HasValue || !to.HasValue)
                throw new ArgumentException("Custom requires From and To.");
            if (from.Value.Date > to.Value.Date)
                throw new ArgumentException("From must be <= To.");
            start = from.Value.Date;
            endExclusive = to.Value.Date.AddDays(1);
        }
        else
        {
            // ToDateRange returns inclusive end-of-day; convert to exclusive next-day midnight.
            // When tests inject vietnamToday, re-resolve against that fixed day.
            var (s, eInclusive) = ResolvePresetAgainstToday(preset, today, firstOrderDate);
            start = s.Date;
            endExclusive = eInclusive.Date.AddDays(1);
        }

        var days = (endExclusive - start).TotalDays;
        // AllTime is offered in the UI: clamp to last MaxRangeDays instead of throwing
        // (stores older than 366 days, or empty DB → MinValue first order).
        if (preset == DateRangePreset.AllTime && (days > MaxRangeDays || days <= 0 || start == DateTime.MinValue.Date))
        {
            endExclusive = today.AddDays(1);
            start = endExclusive.AddDays(-MaxRangeDays);
            days = MaxRangeDays;
        }
        else if (days > MaxRangeDays)
        {
            throw new ArgumentException($"Khoảng thời gian tối đa {MaxRangeDays} ngày.");
        }

        if (days <= 0)
            throw new ArgumentException("Khoảng thời gian không hợp lệ.");

        var prevEnd = start;
        var prevStart = start.AddDays(-days);
        var currentLabel = $"{start:dd/MM/yyyy} – {endExclusive.AddDays(-1):dd/MM/yyyy}";
        var prevLabel = $"{prevStart:dd/MM/yyyy} – {prevEnd.AddDays(-1):dd/MM/yyyy}";

        return new AnalyticsPeriodPair(
            new AnalyticsDateRange(start, endExclusive, currentLabel),
            new AnalyticsDateRange(prevStart, prevEnd, prevLabel));
    }

    private static (DateTime Start, DateTime End) ResolvePresetAgainstToday(
        DateRangePreset preset, DateTime today, DateTime? firstOrderDate)
    {
        return preset switch
        {
            DateRangePreset.Today => (today, today.AddDays(1).AddTicks(-1)),
            DateRangePreset.Yesterday => (today.AddDays(-1), today.AddTicks(-1)),
            DateRangePreset.Last7Days => (today.AddDays(-6), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.Last30Days => (today.AddDays(-29), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.ThisMonth => (new DateTime(today.Year, today.Month, 1), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.LastMonth => DateRangePresetExtensions.GetLastMonthRange(today),
            DateRangePreset.LastWeek => DateRangePresetExtensions.GetLastWeekRange(today),
            DateRangePreset.ThisYear => (new DateTime(today.Year, 1, 1), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.AllTime => (firstOrderDate?.Date ?? DateTime.MinValue.Date, today.AddDays(1).AddTicks(-1)),
            _ => throw new ArgumentException($"Unknown preset: {preset}")
        };
    }

    public static bool InRange(DateTime createdAt, AnalyticsDateRange range) =>
        createdAt >= range.StartInclusive && createdAt < range.EndExclusive;
}
