using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class RevenueController : Controller
    {
        private readonly IRevenueStatisticsService _revenueService;
        private readonly ICancelledOrdersStatisticsService _cancelledOrdersService;

        public RevenueController(
            IRevenueStatisticsService revenueService,
            ICancelledOrdersStatisticsService cancelledOrdersService)
        {
            _revenueService = revenueService;
            _cancelledOrdersService = cancelledOrdersService;
        }

        /// <summary>
        /// Legacy export URL → Analytics hub export.
        /// GET: /Admin/Revenue/ExportReport
        /// </summary>
        [HttpGet]
        public IActionResult ExportReport(DateRangePreset? preset = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            return RedirectToAction("Export", "Analytics", new
            {
                area = "Admin",
                preset = preset ?? DateRangePreset.Last30Days,
                from = startDate,
                to = endDate
            });
        }

        /// <summary>
        /// Legacy revenue index → Analytics hub overview.
        /// GET: /Admin/Revenue
        /// </summary>
        [HttpGet]
        public IActionResult Index() =>
            RedirectToAction("Index", "Analytics", new { area = "Admin", tab = "overview" });

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> FilterByPreset([FromBody] RevenueFilterRequest request)
        {
            DateTime startDate, endDate;
            
            var preset = request.GetPresetEnum();
            
            if (preset == null || preset == DateRangePreset.Custom)
            {
                if (!request.StartDate.HasValue || !request.EndDate.HasValue)
                {
                    return BadRequest(new { error = "Vui lòng chọn ngày bắt đầu và kết thúc." });
                }
                startDate = request.StartDate.Value;
                endDate = request.EndDate.Value.AddDays(1).AddTicks(-1);
            }
            else if (preset == DateRangePreset.AllTime)
            {
                var firstOrderDate = await GetFirstOrderDateAsync();
                (startDate, endDate) = preset.Value.ToDateRange(firstOrderDate);
            }
            else
            {
                (startDate, endDate) = preset.Value.ToDateRange();
            }

            var revenueResult = await _revenueService.GetRevenueByDateRangeAsync(startDate, endDate);
            
            if (!revenueResult.IsValid)
            {
                return BadRequest(new { error = revenueResult.ErrorMessage });
            }

            var categoryRevenue = await _revenueService.GetRevenueByCategoryAsync(startDate, endDate);
            var topProducts = await _revenueService.GetTopProductsAsync(10, startDate, endDate, request.CategoryId);
            var trend = await _revenueService.GetRevenueTrendAsync(TrendPeriod.Daily, startDate, endDate);

            return Json(new
            {
                overview = revenueResult.Data,
                categoryRevenue = categoryRevenue,
                topProducts = topProducts,
                trend = trend
            });
        }

        private async Task<DateTime?> GetFirstOrderDateAsync()
        {
            // Get first order date from service using reflection
            var serviceType = _revenueService.GetType();
            var method = serviceType.GetMethod("GetFirstOrderDateAsync");
            if (method != null)
            {
                var task = method.Invoke(_revenueService, null) as Task<DateTime?>;
                return task != null ? await task : null;
            }
            return null;
        }

        /// <summary>
        /// API lấy xu hướng doanh thu theo period
        /// GET: /Admin/Revenue/RevenueTrend
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RevenueTrend(TrendPeriod period = TrendPeriod.Daily, DateTime? startDate = null, DateTime? endDate = null)
        {
            var trend = await _revenueService.GetRevenueTrendAsync(period, startDate, endDate);
            return Json(trend);
        }

        /// <summary>
        /// Legacy cancelled-orders page → Analytics hub cancellations tab.
        /// GET: /Admin/Revenue/CancelledOrders
        /// </summary>
        [HttpGet]
        public IActionResult CancelledOrders() =>
            RedirectToAction("Index", "Analytics", new { area = "Admin", tab = "cancellations" });

        /// <summary>
        /// API lấy xu hướng đơn hủy
        /// GET: /Admin/Revenue/CancelledOrdersTrend
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CancelledOrdersTrend(TrendPeriod period = TrendPeriod.Daily, DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = await _cancelledOrdersService.GetTrendAsync(period, startDate, endDate);
            
            if (!result.IsValid)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Json(result.Data);
        }

        /// <summary>
        /// API lấy thống kê lý do hủy
        /// GET: /Admin/Revenue/CancelReasonStats
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CancelReasonStats(DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = await _cancelledOrdersService.GetReasonStatisticsAsync(startDate, endDate);
            
            if (!result.IsValid)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Json(result.Data);
        }

        /// <summary>
        /// API lọc đơn hủy theo preset
        /// POST: /Admin/Revenue/FilterCancelledOrders
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> FilterCancelledOrders([FromBody] RevenueFilterRequest request)
        {
            DateTime? startDate = null;
            DateTime? endDate = null;
            
            var preset = request.GetPresetEnum();
            
            if (preset == null || preset == DateRangePreset.Custom)
            {
                if (!request.StartDate.HasValue || !request.EndDate.HasValue)
                {
                    return BadRequest(new { error = "Vui lòng chọn ngày bắt đầu và kết thúc." });
                }
                startDate = request.StartDate.Value;
                endDate = request.EndDate.Value.AddDays(1).AddTicks(-1);
            }
            else if (preset != DateRangePreset.AllTime)
            {
                (startDate, endDate) = preset.Value.ToDateRange();
            }

            var overviewResult = await _cancelledOrdersService.GetOverviewAsync(startDate, endDate);
            
            if (!overviewResult.IsValid)
            {
                return BadRequest(new { error = overviewResult.ErrorMessage });
            }

            var trendResult = await _cancelledOrdersService.GetTrendAsync(TrendPeriod.Daily, startDate, endDate);
            var reasonResult = await _cancelledOrdersService.GetReasonStatisticsAsync(startDate, endDate);

            return Json(new
            {
                overview = overviewResult.Data,
                trend = trendResult.Data,
                reasons = reasonResult.Data
            });
        }
    }

    public class RevenueFilterRequest
    {
        public string? Preset { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CategoryId { get; set; }
        
        public DateRangePreset? GetPresetEnum()
        {
            if (string.IsNullOrEmpty(Preset))
                return null;
            
            if (Enum.TryParse<DateRangePreset>(Preset, true, out var result))
                return result;
            
            return null;
        }
    }

    public class RevenueIndexViewModel
    {
        public RevenueOverviewViewModel Overview { get; set; } = new();
        public RevenueByCategoryViewModel CategoryRevenue { get; set; } = new();
        public TopProductsViewModel TopProducts { get; set; } = new();
        public RevenueTrendViewModel Trend { get; set; } = new();
        public RevenueFilterViewModel Filter { get; set; } = new();
    }
}
