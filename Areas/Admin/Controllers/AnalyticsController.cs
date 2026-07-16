using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AnalyticsController : Controller
    {
        private readonly ISalesAnalyticsService _analytics;

        public AnalyticsController(ISalesAnalyticsService analytics)
        {
            _analytics = analytics;
        }

        /// <summary>
        /// Sales analytics hub (overview / merch / cancellations).
        /// GET: /Admin/Analytics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            DateRangePreset preset = DateRangePreset.Last30Days,
            DateTime? from = null,
            DateTime? to = null,
            string tab = "overview",
            string dimension = "product",
            string? sort = null,
            string? dir = null,
            int take = 50)
        {
            var filter = BuildFilter(preset, from, to, tab, dimension, sort, dir, take);
            var vm = await _analytics.GetHubAsync(filter);
            return View(vm);
        }

        /// <summary>
        /// Export hub data to Excel (implemented in a later task; wired for bookmarks).
        /// GET: /Admin/Analytics/Export
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Export(
            DateRangePreset preset = DateRangePreset.Last30Days,
            DateTime? from = null,
            DateTime? to = null,
            string tab = "overview",
            string dimension = "product",
            string? sort = null,
            string? dir = null,
            int take = 50)
        {
            var filter = BuildFilter(preset, from, to, tab, dimension, sort, dir, take);
            var bytes = await _analytics.ExportExcelAsync(filter);
            var fileName = $"SalesAnalytics_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static SalesAnalyticsFilterVm BuildFilter(
            DateRangePreset preset,
            DateTime? from,
            DateTime? to,
            string? tab,
            string? dimension,
            string? sort,
            string? dir,
            int take)
        {
            return new SalesAnalyticsFilterVm
            {
                Preset = preset,
                From = from,
                To = to,
                Tab = tab?.ToLowerInvariant() switch
                {
                    "merch" => SalesAnalyticsTab.Merch,
                    "cancellations" or "cancel" => SalesAnalyticsTab.Cancellations,
                    _ => SalesAnalyticsTab.Overview
                },
                Dimension = dimension?.ToLowerInvariant() == "category"
                    ? MerchDimension.Category
                    : MerchDimension.Product,
                Sort = sort,
                Dir = dir,
                Take = take
            };
        }
    }
}
