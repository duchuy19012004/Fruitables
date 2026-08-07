namespace Fruitables.ViewModels;

public enum DateRangePreset
{
    Today,
    Yesterday,
    Last7Days,
    LastWeek,
    Last30Days,
    ThisMonth,
    LastMonth,
    ThisYear,
    AllTime,
    Custom
}

public static class DateRangePresetExtensions
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }

    public static (DateTime Start, DateTime End) GetLastWeekRange(DateTime today)
    {
        var daysSinceMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        var thisMonday = today.AddDays(-daysSinceMonday);
        return (thisMonday.AddDays(-7), thisMonday.AddTicks(-1));
    }

    public static (DateTime Start, DateTime End) GetLastMonthRange(DateTime today)
    {
        var firstDayThisMonth = new DateTime(today.Year, today.Month, 1);
        var lastDayLastMonth = firstDayThisMonth.AddTicks(-1);
        var firstDayLastMonth = new DateTime(lastDayLastMonth.Year, lastDayLastMonth.Month, 1);
        return (firstDayLastMonth, lastDayLastMonth);
    }

    public static DateTime GetVietnamToday() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone).Date;
}
