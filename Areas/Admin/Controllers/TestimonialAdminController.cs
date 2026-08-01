using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[RequirePermission("reviews.analyze")]
public class TestimonialAdminController : Controller
{
    private readonly ITestimonialService _testimonialService;
    private readonly ILogger<TestimonialAdminController> _logger;

    public TestimonialAdminController(ITestimonialService testimonialService, ILogger<TestimonialAdminController> logger)
    {
        _testimonialService = testimonialService;
        _logger = logger;
    }

    // Danh sách testimonial (đề xuất chờ duyệt + đã active)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var items = await _testimonialService.GetAllAsync();
            return View(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading testimonial admin page");
            TempData["Error"] = "Có lỗi xảy ra khi tải danh sách testimonial";
            return View(new List<Fruitables.Models.Testimonial>());
        }
    }

    // Duyệt / gỡ testimonial
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool active)
    {
        try
        {
            var success = await _testimonialService.SetActiveAsync(id, active);
            if (!success) return BadRequest(new { success = false, message = "Không tìm thấy testimonial" });
            return Ok(new { success = true, message = active ? "Đã kích hoạt testimonial" : "Đã gỡ testimonial" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling testimonial {Id}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    // Xóa testimonial
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var success = await _testimonialService.DeleteAsync(id);
            if (!success) return BadRequest(new { success = false, message = "Không tìm thấy testimonial" });
            return Ok(new { success = true, message = "Đã xóa testimonial" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting testimonial {Id}", id);
            return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var id) ? id : 0;
    }
}
