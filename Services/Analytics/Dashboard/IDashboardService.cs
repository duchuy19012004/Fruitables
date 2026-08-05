using Fruitables.ViewModels;

namespace Fruitables.Services.Analytics.Dashboard;

/// <summary>
/// Interface cho Dashboard Service - cung cấp dữ liệu thống kê cho Admin Dashboard
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Lấy toàn bộ dữ liệu thống kê cho Dashboard
    /// </summary>
    /// <param name="chartPeriod">Khoảng thời gian cho biểu đồ</param>
    /// <param name="lowStockThreshold">Ngưỡng cảnh báo sắp hết hàng (mặc định 10)</param>
    /// <returns>DashboardViewModel chứa tất cả thống kê</returns>
    Task<DashboardViewModel> GetDashboardDataAsync(ChartPeriod chartPeriod = ChartPeriod.Last7Days, int lowStockThreshold = 10);

    /// <summary>
    /// Lấy thống kê doanh thu
    /// </summary>
    Task<RevenueStatistics> GetRevenueStatisticsAsync();

    /// <summary>
    /// Lấy thống kê đơn hàng
    /// </summary>
    Task<OrderStatistics> GetOrderStatisticsAsync();

    /// <summary>
    /// Lấy thống kê tồn kho
    /// </summary>
    /// <param name="lowStockThreshold">Ngưỡng cảnh báo sắp hết hàng</param>
    Task<InventoryStatistics> GetInventoryStatisticsAsync(int lowStockThreshold = 10);

    /// <summary>
    /// Lấy dữ liệu biểu đồ tăng trưởng
    /// </summary>
    /// <param name="period">Khoảng thời gian</param>
    Task<GrowthChartData> GetGrowthChartDataAsync(ChartPeriod period);
}
