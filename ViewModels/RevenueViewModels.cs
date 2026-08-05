namespace Fruitables.ViewModels;

/// <summary>
/// Các preset lọc theo khoảng thời gian.
/// - Rolling Window: Khoảng thời gian trượt từ ngày hiện tại (Last7Days, Last30Days)
/// - Calendar Period: Kỳ lịch cố định đã chốt sổ (Yesterday, LastWeek, LastMonth)
/// </summary>
public enum DateRangePreset
{
    /// <summary>Hôm nay: 00:00:00 - 23:59:59 ngày hiện tại</summary>
    Today,
    
    /// <summary>Hôm qua: 00:00:00 - 23:59:59 ngày hôm qua (Calendar - đã chốt sổ)</summary>
    Yesterday,
    
    /// <summary>7 ngày gần nhất: [Hôm nay - 6 ngày] đến [Hôm nay] (Rolling)</summary>
    Last7Days,
    
    /// <summary>Tuần trước: Thứ 2 - Chủ nhật tuần trước (Calendar - đã chốt sổ)</summary>
    LastWeek,
    
    /// <summary>30 ngày gần nhất: [Hôm nay - 29 ngày] đến [Hôm nay] (Rolling)</summary>
    Last30Days,
    
    /// <summary>Tháng này: Ngày 1 tháng hiện tại đến ngày hiện tại</summary>
    ThisMonth,
    
    /// <summary>Tháng trước: Ngày 1 - ngày cuối tháng trước (Calendar - đã chốt sổ)</summary>
    LastMonth,
    
    /// <summary>Năm nay: 1/1 năm hiện tại đến ngày hiện tại</summary>
    ThisYear,
    
    /// <summary>Tất cả: Từ đơn hàng đầu tiên đến ngày hiện tại</summary>
    AllTime,
    
    /// <summary>Tùy chọn: Admin tự chọn ngày bắt đầu và kết thúc</summary>
    Custom
}

/// <summary>
/// Enum cho chu kỳ xu hướng
/// </summary>
public enum TrendPeriod
{
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// ViewModel tổng quan doanh thu
/// </summary>
public class RevenueOverviewViewModel
{
    public decimal TotalRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal WeeklyRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal YearlyRevenue { get; set; }
    public decimal MonthlyGrowthPercent { get; set; }
    public decimal WeeklyGrowthPercent { get; set; }
    public int TotalOrders { get; set; }
    public int TodayOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
}

/// <summary>
/// ViewModel cho bộ lọc doanh thu
/// </summary>
public class RevenueFilterViewModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? CategoryId { get; set; }
    public DateRangePreset? Preset { get; set; }
}


/// <summary>
/// ViewModel thống kê doanh thu theo danh mục
/// </summary>
public class RevenueByCategoryViewModel
{
    public List<CategoryRevenueItem> Categories { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public RevenueFilterViewModel Filter { get; set; } = new();
}

/// <summary>
/// Item doanh thu của một danh mục
/// </summary>
public class CategoryRevenueItem
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal Percentage { get; set; }
    public int OrderCount { get; set; }
}

/// <summary>
/// ViewModel danh sách sản phẩm bán chạy
/// </summary>
public class TopProductsViewModel
{
    public List<TopProductItem> Products { get; set; } = new();
    public int TopCount { get; set; }
    public RevenueFilterViewModel Filter { get; set; } = new();
}

/// <summary>
/// Item sản phẩm bán chạy
/// </summary>
public class TopProductItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal AveragePrice { get; set; }
}

/// <summary>
/// ViewModel xu hướng doanh thu
/// </summary>
public class RevenueTrendViewModel
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> RevenueData { get; set; } = new();
    public List<int> OrdersData { get; set; } = new();
    public TrendPeriod Period { get; set; }
}

/// <summary>
/// ViewModel so sánh doanh thu giữa các kỳ
/// </summary>
public class PeriodComparisonViewModel
{
    public decimal CurrentPeriodRevenue { get; set; }
    public decimal PreviousPeriodRevenue { get; set; }
    public decimal GrowthPercent { get; set; }
    public decimal GrowthAmount { get; set; }
    public int CurrentPeriodOrders { get; set; }
    public int PreviousPeriodOrders { get; set; }
    public string CurrentPeriodLabel { get; set; } = string.Empty;
    public string PreviousPeriodLabel { get; set; } = string.Empty;
}

/// <summary>
/// Result type cho GetRevenueByDateRangeAsync - chứa kết quả hoặc lỗi validation
/// </summary>
public class RevenueByDateRangeResult
{
    /// <summary>
    /// Dữ liệu doanh thu (null nếu có lỗi validation)
    /// </summary>
    public RevenueOverviewViewModel? Data { get; private set; }

    /// <summary>
    /// Có lỗi validation hay không
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Thông báo lỗi validation (null nếu không có lỗi)
    /// </summary>
    public string? ErrorMessage { get; private set; }

    private RevenueByDateRangeResult() { }

    /// <summary>
    /// Tạo result thành công với dữ liệu
    /// </summary>
    public static RevenueByDateRangeResult Success(RevenueOverviewViewModel data)
    {
        return new RevenueByDateRangeResult
        {
            Data = data,
            IsValid = true,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Tạo result lỗi validation
    /// </summary>
    public static RevenueByDateRangeResult ValidationError(string errorMessage)
    {
        return new RevenueByDateRangeResult
        {
            Data = null,
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Extension methods cho DateRangePreset để chuyển đổi preset thành khoảng thời gian cụ thể.
/// Sử dụng múi giờ Asia/Ho_Chi_Minh (SE Asia Standard Time).
/// </summary>
public static class DateRangePresetExtensions
{
    /// <summary>
    /// Múi giờ Việt Nam (UTC+7)
    /// </summary>
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    /// <summary>
    /// Lấy TimeZoneInfo cho múi giờ Việt Nam, hỗ trợ cả Windows và Linux
    /// </summary>
    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            // Windows timezone ID
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            // Linux/macOS timezone ID
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }

    /// <summary>
    /// Chuyển đổi DateRangePreset thành khoảng thời gian (StartDate, EndDate).
    /// </summary>
    /// <param name="preset">Preset cần chuyển đổi</param>
    /// <param name="firstOrderDate">Ngày đơn hàng đầu tiên (dùng cho AllTime preset)</param>
    /// <returns>Tuple chứa ngày bắt đầu và ngày kết thúc</returns>
    /// <exception cref="ArgumentException">Khi preset là Custom hoặc không hợp lệ</exception>
    public static (DateTime Start, DateTime End) ToDateRange(
        this DateRangePreset preset,
        DateTime? firstOrderDate = null)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        var today = now.Date;

        return preset switch
        {
            // Hôm nay: 00:00:00 - 23:59:59.9999999
            DateRangePreset.Today => (today, today.AddDays(1).AddTicks(-1)),

            // Hôm qua (Calendar): 00:00:00 - 23:59:59.9999999 ngày hôm qua
            DateRangePreset.Yesterday => (today.AddDays(-1), today.AddTicks(-1)),

            // 7 ngày gần nhất (Rolling): [Hôm nay - 6] đến [Hôm nay 23:59:59]
            DateRangePreset.Last7Days => (today.AddDays(-6), today.AddDays(1).AddTicks(-1)),

            // Tuần trước (Calendar): Thứ 2 - Chủ nhật tuần trước
            DateRangePreset.LastWeek => GetLastWeekRange(today),

            // 30 ngày gần nhất (Rolling): [Hôm nay - 29] đến [Hôm nay 23:59:59]
            DateRangePreset.Last30Days => (today.AddDays(-29), today.AddDays(1).AddTicks(-1)),

            // Tháng này: Ngày 1 đến ngày hiện tại 23:59:59
            DateRangePreset.ThisMonth => (new DateTime(today.Year, today.Month, 1),
                                          today.AddDays(1).AddTicks(-1)),

            // Tháng trước (Calendar): Ngày 1 - ngày cuối tháng trước
            DateRangePreset.LastMonth => GetLastMonthRange(today),

            // Năm nay: 1/1 đến ngày hiện tại 23:59:59
            DateRangePreset.ThisYear => (new DateTime(today.Year, 1, 1),
                                         today.AddDays(1).AddTicks(-1)),

            // Tất cả: Từ đơn hàng đầu tiên đến ngày hiện tại 23:59:59
            DateRangePreset.AllTime => (firstOrderDate ?? DateTime.MinValue,
                                        today.AddDays(1).AddTicks(-1)),

            // Custom không được hỗ trợ - cần truyền StartDate/EndDate trực tiếp
            DateRangePreset.Custom => throw new ArgumentException(
                "Custom preset requires explicit StartDate and EndDate parameters.", nameof(preset)),

            _ => throw new ArgumentException($"Unknown preset: {preset}", nameof(preset))
        };
    }

    /// <summary>
    /// Tính khoảng thời gian tuần trước (Thứ 2 đến Chủ nhật).
    /// Tuần bắt đầu từ Thứ 2 (Monday).
    /// </summary>
    /// <param name="today">Ngày hiện tại</param>
    /// <returns>Tuple chứa ngày Thứ 2 và Chủ nhật 23:59:59 của tuần trước</returns>
    public static (DateTime Start, DateTime End) GetLastWeekRange(DateTime today)
    {
        // Tính số ngày từ Thứ 2 của tuần hiện tại
        // DayOfWeek: Sunday = 0, Monday = 1, ..., Saturday = 6
        // Chuyển đổi để Monday = 0, Sunday = 6
        int daysSinceMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        var thisMonday = today.AddDays(-daysSinceMonday);

        // Tuần trước: Thứ 2 tuần trước đến Chủ nhật tuần trước 23:59:59.9999999
        var lastMonday = thisMonday.AddDays(-7);
        var lastSunday = thisMonday.AddTicks(-1); // 23:59:59.9999999 Chủ nhật

        return (lastMonday, lastSunday);
    }

    /// <summary>
    /// Tính khoảng thời gian tháng trước (ngày 1 đến ngày cuối tháng).
    /// </summary>
    /// <param name="today">Ngày hiện tại</param>
    /// <returns>Tuple chứa ngày đầu và ngày cuối 23:59:59 của tháng trước</returns>
    public static (DateTime Start, DateTime End) GetLastMonthRange(DateTime today)
    {
        // Ngày đầu tháng này
        var firstDayThisMonth = new DateTime(today.Year, today.Month, 1);

        // Ngày cuối tháng trước = ngày đầu tháng này - 1 tick
        var lastDayLastMonth = firstDayThisMonth.AddTicks(-1);

        // Ngày đầu tháng trước
        var firstDayLastMonth = new DateTime(lastDayLastMonth.Year, lastDayLastMonth.Month, 1);

        return (firstDayLastMonth, lastDayLastMonth);
    }

    /// <summary>
    /// Lấy thời gian hiện tại theo múi giờ Việt Nam.
    /// </summary>
    /// <returns>DateTime hiện tại theo múi giờ Việt Nam</returns>
    public static DateTime GetVietnamNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    }

    /// <summary>
    /// Lấy ngày hôm nay (00:00:00) theo múi giờ Việt Nam.
    /// </summary>
    /// <returns>DateTime ngày hôm nay 00:00:00 theo múi giờ Việt Nam</returns>
    public static DateTime GetVietnamToday()
    {
        return GetVietnamNow().Date;
    }
}
