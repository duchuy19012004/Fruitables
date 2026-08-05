using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Reviews;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[RequirePermission("reviews.view")]
public class ReviewAdminController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewAdminController> _logger;

    public ReviewAdminController(IReviewService reviewService, ILogger<ReviewAdminController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    /// <summary>
    /// Trang quản lý reviews
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(ReviewAdminFilterDto filter)
    {
        try
        {
            var result = await _reviewService.GetAllReviewsAsync(filter);
            var stats = await _reviewService.GetAdminStatisticsAsync();
            
            ViewBag.AverageRating = stats.AverageRating;
            ViewBag.TotalValidReviews = stats.ValidReviews;
            ViewBag.NewReviewsToday = stats.ReviewsToday;
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ReviewTable", result);
            }
            
            return View(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reviews admin page");
            TempData["Error"] = "Có lỗi xảy ra khi tải danh sách đánh giá";
            return View(new PagedResult<ReviewAdminViewModel>());
        }
    }

    /// <summary>
    /// Ẩn review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.moderate")]
    public async Task<IActionResult> Hide(int id, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập lý do ẩn" });
            }

            var adminId = GetCurrentUserId();
            var success = await _reviewService.HideReviewAsync(id, reason, adminId);

            if (!success)
            {
                return BadRequest(new { success = false, message = "Không thể ẩn đánh giá" });
            }

            return Ok(new { success = true, message = "Đánh giá đã được ẩn" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding review {ReviewId}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    /// <summary>
    /// Hiện review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.moderate")]
    public async Task<IActionResult> Show(int id)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var success = await _reviewService.ShowReviewAsync(id, adminId);

            if (!success)
            {
                return BadRequest(new { success = false, message = "Không thể hiện đánh giá" });
            }

            return Ok(new { success = true, message = "Đánh giá đã được hiện" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing review {ReviewId}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    /// <summary>
    /// Xóa review (admin)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.delete")]
    public async Task<IActionResult> Delete(int id, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập lý do xóa" });
            }

            var adminId = GetCurrentUserId();
            var success = await _reviewService.DeleteReviewByAdminAsync(id, reason, adminId);

            if (!success)
            {
                return BadRequest(new { success = false, message = "Không thể xóa đánh giá" });
            }

            return Ok(new { success = true, message = "Đánh giá đã được xóa" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    /// <summary>
    /// Trang quản lý reports
    /// </summary>
    [HttpGet]
    [RequirePermission("reviews.view_reports")]
    public async Task<IActionResult> Reports(ReportFilterDto filter)
    {
        try
        {
            var result = await _reviewService.GetReviewReportsAsync(filter);
            return View(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reports page");
            TempData["Error"] = "Có lỗi xảy ra khi tải danh sách báo cáo";
            return View(new PagedResult<ReviewReportViewModel>());
        }
    }

    /// <summary>
    /// Xử lý report
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("reviews.moderate")]
    public async Task<IActionResult> HandleReport(int id, ReportAction action)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var success = await _reviewService.HandleReportAsync(id, action, adminId);

            if (!success)
            {
                return BadRequest(new { success = false, message = "Không thể xử lý báo cáo" });
            }

            var message = action == ReportAction.Resolve 
                ? "Báo cáo đã được xử lý và đánh giá đã bị ẩn" 
                : "Báo cáo đã được bỏ qua";

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling report {ReportId}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    /// <summary>
    /// Trang thống kê
    /// </summary>
    [HttpGet]
    [RequirePermission("reviews.view_statistics")]
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var stats = await _reviewService.GetAdminStatisticsAsync();
            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading statistics page");
            TempData["Error"] = "Có lỗi xảy ra khi tải thống kê";
            return View(new ReviewAdminStatistics());
        }
    }

    /// <summary>
    /// API: Lấy thống kê (JSON)
    /// </summary>
    [HttpGet]
    [RequirePermission("reviews.view_statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var stats = await _reviewService.GetAdminStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statistics");
            return StatusCode(500, new { error = "Có lỗi xảy ra" });
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }
}
