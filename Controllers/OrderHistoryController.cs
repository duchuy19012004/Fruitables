using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Fruitables.Models;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Controllers;

// Controller lịch sử đơn hàng: danh sách, lọc, chi tiết, hủy đơn.
// Yêu cầu đăng nhập ([Authorize]).
[Authorize]
public class OrderHistoryController : Controller
{
    private readonly IOrderHistoryService _orderHistoryService;
    private readonly IMemoryCache _cache;
    private readonly IReturnEligibilityService? _returnEligibility;
    // Cache key cho dropdown trạng thái đơn hàng
    private const string OrderStatusesCacheKey = "OrderHistory_Statuses";
    private const int MaxPageSize = 50;
    private const int MaxSearchTermLength = 50;

    // Inject service lịch sử đơn hàng + memory cache (cho dropdown status)
    public OrderHistoryController(IOrderHistoryService orderHistoryService, IMemoryCache cache, IReturnEligibilityService? returnEligibility = null)
    {
        _orderHistoryService = orderHistoryService;
        _cache = cache;
        _returnEligibility = returnEligibility;
    }

    // GET: /OrderHistory — danh sách đơn hàng + phân trang + lọc
    [HttpGet]
    public async Task<IActionResult> Index(OrderHistoryFilterViewModel filter)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // Validate + sanitize input trước khi query
        filter = SanitizeFilter(filter);

        var result = await _orderHistoryService.GetOrderHistoryAsync(userId.Value, filter);

        ViewBag.Filter = filter;
        ViewBag.Statuses = GetOrderStatusesCached();

        return View(result);
    }

    // GET: /OrderHistory/Filter — AJAX endpoint lọc real-time, trả PartialView
    [HttpGet]
    public async Task<IActionResult> Filter(OrderHistoryFilterViewModel filter)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            // AJAX request → trả 401, không redirect
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Unauthorized();
            }
            return RedirectToAction("Login", "Account");
        }

        // Validate + sanitize input
        filter = SanitizeFilter(filter);

        try
        {
            var result = await _orderHistoryService.GetOrderHistoryAsync(userId.Value, filter);

            ViewBag.Filter = filter;

            return PartialView("_OrderListContainer", result);
        }
        // Bắt exception chung → trả error partial
        catch (Exception)
        {
            return PartialView("_OrderListError", "Có lỗi xảy ra khi tải danh sách đơn hàng. Vui lòng thử lại.");
        }
    }

    // GET: /OrderHistory/Details/{id} — chi tiết đơn hàng
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var orderDetail = await _orderHistoryService.GetOrderDetailAsync(id, userId.Value);
        // Kiểm tra tồn tại + quyền sở hữu
        if (orderDetail == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.";
            return RedirectToAction(nameof(Index));
        }

        if (_returnEligibility != null && orderDetail.Status == OrderStatus.Delivered)
        {
            ViewBag.ReturnEligibility = await _returnEligibility.CheckOrderAsync(id, userId.Value);
        }
        return View(orderDetail);
    }

    // POST: /OrderHistory/Cancel/{id} — hủy đơn hàng
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string cancelReason)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        // Validate ID đơn hàng
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Mã đơn hàng không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        // Sanitize + validate lý do hủy (chống XSS, tối thiểu 5 ký tự)
        var sanitizedReason = SanitizeCancelReason(cancelReason);
        if (string.IsNullOrWhiteSpace(sanitizedReason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do hủy đơn hàng (tối thiểu 5 ký tự).";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _orderHistoryService.CancelOrderAsync(id, userId.Value, sanitizedReason);
        if (!result)
        {
            TempData["ErrorMessage"] = "Không thể hủy đơn hàng. Đơn hàng có thể đã được xử lý hoặc bạn không có quyền hủy.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Đơn hàng đã được hủy thành công.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Sanitize lý do hủy: trim, giới hạn 500 ký tự, yêu cầu >= 5 ký tự, encode HTML
    private static string? SanitizeCancelReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var sanitized = reason.Trim();
        if (sanitized.Length > 500)
        {
            sanitized = sanitized[..500];
        }

        // Tối thiểu 5 ký tự (sau trim)
        if (sanitized.Length < 5)
        {
            return null;
        }

        // Encode HTML entities chống XSS
        sanitized = sanitized
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");

        return sanitized;
    }

    // Helper: lấy userId từ claims cookie
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    // Lấy danh sách trạng thái đơn hàng có cache (refresh 24h)
    private List<SelectListItem> GetOrderStatusesCached()
    {
        return _cache.GetOrCreate(OrderStatusesCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            entry.Priority = CacheItemPriority.Low;
            return GetOrderStatuses();
        })!;
    }

    // Danh sách trạng thái cho dropdown filter
    private static List<SelectListItem> GetOrderStatuses()
    {
        return new List<SelectListItem>
        {
            new() { Value = "", Text = "Tất cả trạng thái" },
            new() { Value = ((int)OrderStatus.Pending).ToString(), Text = "Chờ xử lý" },
            new() { Value = ((int)OrderStatus.Processing).ToString(), Text = "Đang xử lý" },
            new() { Value = ((int)OrderStatus.Shipped).ToString(), Text = "Đang giao" },
            new() { Value = ((int)OrderStatus.Delivered).ToString(), Text = "Đã giao" },
            new() { Value = ((int)OrderStatus.Cancelled).ToString(), Text = "Đã hủy" }
        };
    }

    // Validate + sanitize filter params chống injection
    private static OrderHistoryFilterViewModel SanitizeFilter(OrderHistoryFilterViewModel filter)
    {
        // Giới hạn page/pageSize
        filter.Page = filter.Page < 1 ? 1 : filter.Page;
        filter.PageSize = filter.PageSize < 1 ? 10 : Math.Min(filter.PageSize, MaxPageSize);

        // SearchTerm: chỉ cho phép alphanumeric + dấu gạch ngang + dấu cách
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.Trim();
            if (searchTerm.Length > MaxSearchTermLength)
            {
                searchTerm = searchTerm[..MaxSearchTermLength];
            }

            filter.SearchTerm = Regex.Replace(searchTerm, @"[^\w\s\-]", "", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        }

        // Status: chỉ chấp nhận giá trị enum hợp lệ
        if (filter.Status.HasValue && !Enum.IsDefined(typeof(OrderStatus), filter.Status.Value))
        {
            filter.Status = null;
        }

        // Date range: nếu FromDate > ToDate thì swap
        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
        {
            (filter.FromDate, filter.ToDate) = (filter.ToDate, filter.FromDate);
        }

        return filter;
    }
}

// Helper class cho dropdown trong View
public class SelectListItem
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
