using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.ViewModels;

namespace Fruitables.Areas.Admin.Controllers
{
    /// <summary>
    /// Legacy revenue routes — redirect-only to the Sales Analytics hub.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class RevenueController : Controller
    {
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

        /// <summary>
        /// Legacy cancelled-orders page → Analytics hub cancellations tab.
        /// GET: /Admin/Revenue/CancelledOrders
        /// </summary>
        [HttpGet]
        public IActionResult CancelledOrders() =>
            RedirectToAction("Index", "Analytics", new { area = "Admin", tab = "cancellations" });
    }
}
