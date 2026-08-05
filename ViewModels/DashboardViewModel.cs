namespace Fruitables.ViewModels;

/// <summary>
/// ViewModel chứa dữ liệu thống kê tổng quan cho Dashboard
/// </summary>
public class DashboardViewModel
{
    /// <summary>
    /// Thống kê doanh thu
    /// </summary>
    public RevenueStatistics Revenue { get; set; } = new();

    /// <summary>
    /// Thống kê đơn hàng
    /// </summary>
    public OrderStatistics Orders { get; set; } = new();

    /// <summary>
    /// Thống kê sản phẩm tồn kho
    /// </summary>
    public InventoryStatistics Inventory { get; set; } = new();

    /// <summary>
    /// Dữ liệu biểu đồ tăng trưởng
    /// </summary>
    public GrowthChartData GrowthChart { get; set; } = new();

    /// <summary>
    /// Danh sách đơn hàng gần đây
    /// </summary>
    public List<RecentOrderItem> RecentOrders { get; set; } = new();

    /// <summary>
    /// Thống kê cảm xúc review (module phân tích cảm xúc)
    /// </summary>
    public DashboardSentimentStatistics Sentiment { get; set; } = new();
}

/// <summary>
/// Thống kê cảm xúc review hiển thị trên Dashboard admin
/// </summary>
public class DashboardSentimentStatistics
{
    /// <summary>
    /// Số review tiêu cực 7 ngày gần nhất (đã phân tích)
    /// </summary>
    public int Negative7d { get; set; }

    /// <summary>
    /// Tổng review phân tích 7 ngày gần nhất
    /// </summary>
    public int Total7d { get; set; }

    /// <summary>
    /// Số cảnh báo tiêu cực nghiêm trọng chưa xử lý
    /// </summary>
    public int PendingAlerts { get; set; }

    /// <summary>
    /// Số review đang chờ admin duyệt do conflict hoặc rủi ro.
    /// </summary>
    public int PendingReviews { get; set; }
}

/// <summary>
/// Thông tin đơn hàng gần đây cho Dashboard
/// </summary>
public class RecentOrderItem
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Thống kê doanh thu
/// </summary>
public class RevenueStatistics
{
    /// <summary>
    /// Tổng doanh thu (tất cả thời gian)
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Doanh thu hôm nay
    /// </summary>
    public decimal TodayRevenue { get; set; }

    /// <summary>
    /// Doanh thu tháng này
    /// </summary>
    public decimal MonthlyRevenue { get; set; }

    /// <summary>
    /// Phần trăm thay đổi so với tháng trước
    /// </summary>
    public decimal MonthlyGrowthPercent { get; set; }
}

/// <summary>
/// Thống kê đơn hàng
/// </summary>
public class OrderStatistics
{
    /// <summary>
    /// Tổng số đơn hàng
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// Số đơn hàng hôm nay
    /// </summary>
    public int TodayOrders { get; set; }

    /// <summary>
    /// Số đơn đang chờ xử lý (Pending)
    /// </summary>
    public int PendingOrders { get; set; }

    /// <summary>
    /// Số đơn đang xử lý (Processing)
    /// </summary>
    public int ProcessingOrders { get; set; }

    /// <summary>
    /// Số đơn đã giao (Delivered)
    /// </summary>
    public int DeliveredOrders { get; set; }

    /// <summary>
    /// Số đơn đã hủy (Cancelled)
    /// </summary>
    public int CancelledOrders { get; set; }
}

/// <summary>
/// Thống kê tồn kho
/// </summary>
public class InventoryStatistics
{
    /// <summary>
    /// Tổng số sản phẩm
    /// </summary>
    public int TotalProducts { get; set; }

    /// <summary>
    /// Số sản phẩm đang hoạt động
    /// </summary>
    public int ActiveProducts { get; set; }

    /// <summary>
    /// Số sản phẩm hết hàng (StockQuantity = 0)
    /// </summary>
    public int OutOfStockProducts { get; set; }

    /// <summary>
    /// Số sản phẩm sắp hết hàng (StockQuantity <= LowStockThreshold)
    /// </summary>
    public int LowStockProducts { get; set; }

    /// <summary>
    /// Tổng số lượng tồn kho
    /// </summary>
    public decimal TotalStockQuantity { get; set; }
}

/// <summary>
/// Dữ liệu biểu đồ tăng trưởng
/// </summary>
public class GrowthChartData
{
    /// <summary>
    /// Labels cho trục X (ngày/tháng)
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// Dữ liệu doanh thu theo thời gian
    /// </summary>
    public List<decimal> RevenueData { get; set; } = new();

    /// <summary>
    /// Dữ liệu số đơn hàng theo thời gian
    /// </summary>
    public List<int> OrdersData { get; set; } = new();
}

/// <summary>
/// Enum cho khoảng thời gian biểu đồ
/// </summary>
public enum ChartPeriod
{
    Last7Days,
    Last30Days,
    Last12Months
}
