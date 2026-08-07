using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Reviews;
using Fruitables.ViewModels;

namespace Fruitables.Services.Analytics.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync(
        ChartPeriod chartPeriod = ChartPeriod.Last7Days, 
        int lowStockThreshold = 10)
    {
        var revenue = await GetRevenueStatisticsAsync();
        var orders = await GetOrderStatisticsAsync();
        var inventory = await GetInventoryStatisticsAsync(lowStockThreshold);
        var growthChart = await GetGrowthChartDataAsync(chartPeriod);
        var recentOrders = await GetRecentOrdersAsync(5);
        var sentiment = await GetSentimentStatisticsAsync();

        return new DashboardViewModel
        {
            Revenue = revenue,
            Orders = orders,
            Inventory = inventory,
            GrowthChart = growthChart,
            RecentOrders = recentOrders,
            Sentiment = sentiment
        };
    }

    // Thống kê cảm xúc review cho widget dashboard
    public async Task<DashboardSentimentStatistics> GetSentimentStatisticsAsync()
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-7);
            if (_unitOfWork is Repositories.UnitOfWork concrete && concrete.Context.Database.IsSqlServer())
            {
                var serializer = new VersionedJsonSerializer();
                var reviews = await concrete.Context.Reviews.AsNoTracking()
                    .Where(review => !review.IsDeleted && review.CreatedAt >= since)
                    .ToListAsync();
                var targetRows = reviews
                    .Select(review => ReviewAggregateJson.Read(review, serializer).Sentiment)
                    .Where(sentiment => sentiment is not null)
                    .ToList();
                var targetEligible = targetRows.Where(sentiment => !sentiment!.NeedsManualReview && sentiment.Sentiment != SentimentLabel.Failed).ToList();
                return new DashboardSentimentStatistics
                {
                    Negative7d = targetEligible.Count(sentiment => sentiment!.Sentiment == SentimentLabel.Negative),
                    Total7d = targetEligible.Count,
                    PendingAlerts = targetRows.Count(sentiment => sentiment!.AlertStatus == SentimentAlertStatus.Pending),
                    PendingReviews = targetRows.Count(sentiment => sentiment!.NeedsManualReview)
                };
            }

            var rows = await (
                from r in _unitOfWork.Reviews.Query()
                join s in _unitOfWork.ReviewSentiments.Query() on r.Id equals s.ReviewId
                where !r.IsDeleted && r.CreatedAt >= since
                select new { s.Sentiment, s.NeedsManualReview, s.AlertStatus }).ToListAsync();

            var eligible = rows
                .Where(x => !x.NeedsManualReview && x.Sentiment != Fruitables.Models.SentimentLabel.Failed)
                .ToList();

            var data = new DashboardSentimentStatistics
            {
                Negative7d = eligible.Count(x => x.Sentiment == Fruitables.Models.SentimentLabel.Negative),
                Total7d = eligible.Count,
                PendingAlerts = rows.Count(x => x.AlertStatus == Fruitables.Models.SentimentAlertStatus.Pending),
                PendingReviews = rows.Count(x => x.NeedsManualReview)
            };

            if (data.Total7d == 0)
            {
                // Chưa có review nào phân tích — đếm số review 7 ngày chưa phân tích để gợi ý chạy backfill
                data.Total7d = await _unitOfWork.Reviews.Query().CountAsync(r => !r.IsDeleted && r.CreatedAt >= since);
            }

            return data;
        }
        catch (Exception)
        {
            // Widget cảm xúc là phụ — lỗi không được làm sập dashboard
            return new DashboardSentimentStatistics();
        }
    }

    public async Task<List<RecentOrderItem>> GetRecentOrdersAsync(int count = 5)
    {
        var orders = await _unitOfWork.Orders.Query()
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .Take(count)
            .ToListAsync();

        return orders.Select(o => new RecentOrderItem
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CustomerName = o.User?.Name ?? "Khách vãng lai",
            Total = o.Total,
            Status = GetStatusDisplayName(o.Status),
            StatusBadgeClass = GetStatusBadgeClass(o.Status),
            CreatedAt = o.CreatedAt
        }).ToList();
    }

    private static string GetStatusDisplayName(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xử lý",
        OrderStatus.Processing => "Đang xử lý",
        OrderStatus.Shipped => "Đang giao",
        OrderStatus.Delivered => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    private static string GetStatusBadgeClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bg-secondary",
        OrderStatus.Processing => "bg-info",
        OrderStatus.Shipped => "bg-warning",
        OrderStatus.Delivered => "bg-success",
        OrderStatus.Cancelled => "bg-danger",
        _ => "bg-secondary"
    };

    public async Task<RevenueStatistics> GetRevenueStatisticsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);

        var query = _unitOfWork.Orders.Query()
            .Where(o => o.PaymentStatus == PaymentStatus.Paid);

        // Project counts and sums in a single query
        var stats = await query
            .GroupBy(o => 1)
            .Select(g => new
            {
                Total = g.Sum(o => o.Total),
                Today = g.Sum(o => o.CreatedAt >= today && o.CreatedAt < tomorrow ? o.Total : 0),
                ThisMonth = g.Sum(o => o.CreatedAt >= firstDayOfMonth ? o.Total : 0),
                LastMonth = g.Sum(o => o.CreatedAt >= firstDayOfLastMonth && o.CreatedAt < firstDayOfMonth ? o.Total : 0)
            })
            .FirstOrDefaultAsync();

        var totalRevenue = stats?.Total ?? 0;
        var todayRevenue = stats?.Today ?? 0;
        var monthlyRevenue = stats?.ThisMonth ?? 0;
        var lastMonthRevenue = stats?.LastMonth ?? 0;

        decimal growthPercent = 0;
        if (lastMonthRevenue > 0)
        {
            growthPercent = ((monthlyRevenue - lastMonthRevenue) / lastMonthRevenue) * 100;
        }
        else if (monthlyRevenue > 0)
        {
            growthPercent = 100;
        }

        return new RevenueStatistics
        {
            TotalRevenue = totalRevenue,
            TodayRevenue = todayRevenue,
            MonthlyRevenue = monthlyRevenue,
            MonthlyGrowthPercent = Math.Round(growthPercent, 2)
        };
    }

    public async Task<OrderStatistics> GetOrderStatisticsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var query = _unitOfWork.Orders.Query();

        var stats = await query
            .GroupBy(o => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Today = g.Count(o => o.CreatedAt >= today && o.CreatedAt < tomorrow),
                Pending = g.Count(o => o.Status == OrderStatus.Pending),
                Processing = g.Count(o => o.Status == OrderStatus.Processing),
                Delivered = g.Count(o => o.Status == OrderStatus.Delivered),
                Cancelled = g.Count(o => o.Status == OrderStatus.Cancelled)
            })
            .FirstOrDefaultAsync();

        return new OrderStatistics
        {
            TotalOrders = stats?.Total ?? 0,
            TodayOrders = stats?.Today ?? 0,
            PendingOrders = stats?.Pending ?? 0,
            ProcessingOrders = stats?.Processing ?? 0,
            DeliveredOrders = stats?.Delivered ?? 0,
            CancelledOrders = stats?.Cancelled ?? 0
        };
    }

    public async Task<InventoryStatistics> GetInventoryStatisticsAsync(int lowStockThreshold = 10)
    {
        // Xử lý threshold âm
        var threshold = Math.Max(0, lowStockThreshold);

        var query = _unitOfWork.Products.Query();

        var stats = await query
            .GroupBy(p => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(p => p.IsActive),
                OutOfStock = g.Count(p => p.StockQuantity <= 0),
                LowStock = g.Count(p => p.StockQuantity > 0 && p.StockQuantity <= threshold),
                TotalStock = g.Where(p => p.IsActive).Sum(p => p.StockQuantity)
            })
            .FirstOrDefaultAsync();

        return new InventoryStatistics
        {
            TotalProducts = stats?.Total ?? 0,
            ActiveProducts = stats?.Active ?? 0,
            OutOfStockProducts = stats?.OutOfStock ?? 0,
            LowStockProducts = stats?.LowStock ?? 0,
            TotalStockQuantity = stats?.TotalStock ?? 0
        };
    }

    public async Task<GrowthChartData> GetGrowthChartDataAsync(ChartPeriod period)
    {
        var today = DateTime.UtcNow.Date;
        DateTime minDate = today;

        switch (period)
        {
            case ChartPeriod.Last7Days:
                minDate = today.AddDays(-6);
                break;
            case ChartPeriod.Last30Days:
                minDate = today.AddDays(-29);
                break;
            case ChartPeriod.Last12Months:
                minDate = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
                break;
        }

        var query = _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= minDate);

        var orders = await query
            .Select(o => new OrderDto 
            { 
                CreatedAt = o.CreatedAt, 
                Total = o.Total 
            })
            .ToListAsync();

        var result = new GrowthChartData();

        switch (period)
        {
            case ChartPeriod.Last7Days:
                result = BuildDailyChartData(orders, today, 7);
                break;
            case ChartPeriod.Last30Days:
                result = BuildDailyChartData(orders, today, 30);
                break;
            case ChartPeriod.Last12Months:
                result = BuildMonthlyChartData(orders, today, 12);
                break;
        }

        return result;
    }

    private static GrowthChartData BuildDailyChartData(List<OrderDto> orders, DateTime today, int days)
    {
        var result = new GrowthChartData();

        for (int i = days - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayOrders = orders.Where(o => o.CreatedAt.Date == date).ToList();

            result.Labels.Add(date.ToString("dd/MM"));
            result.RevenueData.Add(dayOrders.Sum(o => o.Total));
            result.OrdersData.Add(dayOrders.Count);
        }

        return result;
    }

    private static GrowthChartData BuildMonthlyChartData(List<OrderDto> orders, DateTime today, int months)
    {
        var result = new GrowthChartData();

        for (int i = months - 1; i >= 0; i--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var monthOrders = orders.Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd).ToList();

            result.Labels.Add(monthStart.ToString("MM/yyyy"));
            result.RevenueData.Add(monthOrders.Sum(o => o.Total));
            result.OrdersData.Add(monthOrders.Count);
        }

        return result;
    }

    private class OrderDto
    {
        public DateTime CreatedAt { get; set; }
        public decimal Total { get; set; }
    }
}
