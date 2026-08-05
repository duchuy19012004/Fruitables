using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Models;
using System.Security.Claims;
using Fruitables.Services.Orders.OrderManagement;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class OrderController : Controller
{
    private readonly IOrderAdminService _orderAdminService;

    public OrderController(IOrderAdminService orderAdminService)
    {
        _orderAdminService = orderAdminService;
    }

    // GET: Admin/Order
    public async Task<IActionResult> Index(
        string? search,
        OrderStatus? status,
        PaymentStatus? paymentStatus,
        DateTime? fromDate,
        DateTime? toDate,
        string? sortBy,
        bool sortDescending = true,
        int page = 1)
    {
        var request = new OrderListRequest
        {
            Search = search,
            Status = status,
            PaymentStatus = paymentStatus,
            FromDate = fromDate,
            ToDate = toDate,
            SortBy = sortBy ?? "CreatedAt",
            SortDescending = sortDescending,
            Page = page,
            PageSize = 10
        };

        var result = await _orderAdminService.GetOrdersAsync(request);

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.PaymentStatus = paymentStatus;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.SortBy = sortBy ?? "CreatedAt";
        ViewBag.SortDescending = sortDescending;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_OrderList", result);
        }

        return View(result);
    }

    // GET: Admin/Order/Detail/{id}
    public async Task<IActionResult> Detail(int id)
    {
        var order = await _orderAdminService.GetOrderWithHistoryAsync(id);

        if (order == null)
        {
            TempData["Error"] = "Không tìm thấy đơn hàng";
            return RedirectToAction(nameof(Index));
        }

        // Load order notes
        var orderNotes = await _orderAdminService.GetOrderNotesAsync(id);
        ViewBag.OrderNotes = orderNotes;

        return View(order);
    }

    // POST: Admin/Order/UpdateStatus
    // Used for individual state transitions (Pending -> Processing -> Shipped -> Delivered)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus newStatus, string? notes)
    {
        var adminId = GetCurrentAdminId();

        var request = new UpdateOrderStatusRequest
        {
            OrderId = orderId,
            NewStatus = newStatus,
            AdminId = adminId,
            Notes = notes
        };

        var result = await _orderAdminService.UpdateOrderStatusAsync(request);

        if (!result.Success)
        {
            TempData["Error"] = GetErrorMessage(result);
            return RedirectToAction(nameof(Detail), new { id = orderId });
        }

        TempData["Success"] = "Cập nhật trạng thái đơn hàng thành công!";
        return RedirectToAction(nameof(Detail), new { id = orderId });
    }

    // POST: Admin/Order/UpdatePaymentStatus
    // Used for individual payment status updates
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentStatus(int orderId, PaymentStatus newPaymentStatus, string? notes)
    {
        var adminId = GetCurrentAdminId();

        var request = new UpdatePaymentStatusRequest
        {
            OrderId = orderId,
            NewPaymentStatus = newPaymentStatus,
            AdminId = adminId,
            Notes = notes
        };

        var result = await _orderAdminService.UpdatePaymentStatusAsync(request);

        if (!result.Success)
        {
            TempData["Error"] = GetErrorMessage(result);
            return RedirectToAction(nameof(Detail), new { id = orderId });
        }

        TempData["Success"] = "Cập nhật trạng thái thanh toán thành công!";
        return RedirectToAction(nameof(Detail), new { id = orderId });
    }

    // POST: Admin/Order/UpdateCombinedStatus
    // Requirements: 1.2, 3.1 - Combined update of OrderStatus and PaymentStatus in a single transaction
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCombinedStatus(UpdateCombinedStatusRequest request)
    {
        var adminId = GetCurrentAdminId();
        
        var result = await _orderAdminService.UpdateCombinedStatusAsync(request, adminId);

        if (!result.Success)
        {
            TempData["Error"] = GetErrorMessage(result);
            return RedirectToAction(nameof(Detail), new { id = request.OrderId });
        }

        TempData["Success"] = "Cập nhật trạng thái thành công!";
        return RedirectToAction(nameof(Detail), new { id = request.OrderId });
    }

    // AJAX: Filter orders (real-time)
    [HttpGet]
    public async Task<IActionResult> Filter(
        string? search,
        OrderStatus? status,
        PaymentStatus? paymentStatus,
        DateTime? fromDate,
        DateTime? toDate,
        string? sortBy,
        bool sortDescending = true,
        int page = 1)
    {
        var request = new OrderListRequest
        {
            Search = search,
            Status = status,
            PaymentStatus = paymentStatus,
            FromDate = fromDate,
            ToDate = toDate,
            SortBy = sortBy ?? "CreatedAt",
            SortDescending = sortDescending,
            Page = page,
            PageSize = 10
        };

        var result = await _orderAdminService.GetOrdersAsync(request);

        var orders = result.Orders.Select(o => new
        {
            o.Id,
            o.OrderNumber,
            CustomerName = o.User?.Name,
            CustomerEmail = o.User?.Email,
            Total = o.Total.ToString("N0"),
            Status = (int)o.Status,
            StatusName = GetStatusDisplayName(o.Status),
            StatusBadgeClass = GetStatusBadgeClass(o.Status),
            PaymentStatus = (int)o.PaymentStatus,
            PaymentStatusName = GetPaymentStatusDisplayName(o.PaymentStatus),
            PaymentStatusBadgeClass = GetPaymentStatusBadgeClass(o.PaymentStatus),
            CreatedAt = o.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        });

        return Json(new
        {
            orders,
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalPages
        });
    }

    private static string GetStatusBadgeClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bg-warning text-dark",
        OrderStatus.Processing => "bg-info",
        OrderStatus.Shipped => "bg-primary",
        OrderStatus.Delivered => "bg-success",
        OrderStatus.Cancelled => "bg-danger",
        _ => "bg-secondary"
    };

    private static string GetPaymentStatusBadgeClass(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "bg-warning text-dark",
        PaymentStatus.Paid => "bg-success",
        PaymentStatus.Refunded => "bg-info",
        _ => "bg-secondary"
    };

    // AJAX: Get valid status transitions
    [HttpGet]
    public IActionResult GetValidStatusTransitions(OrderStatus currentStatus)
    {
        var allStatuses = Enum.GetValues<OrderStatus>();
        var validTransitions = allStatuses
            .Where(s => _orderAdminService.IsValidStatusTransition(currentStatus, s))
            .Select(s => new { value = (int)s, text = GetStatusDisplayName(s) })
            .ToList();

        return Json(validTransitions);
    }

    // AJAX: Get valid payment status transitions
    [HttpGet]
    public IActionResult GetValidPaymentStatusTransitions(PaymentStatus currentPaymentStatus, OrderStatus orderStatus)
    {
        var allStatuses = Enum.GetValues<PaymentStatus>();
        var validTransitions = allStatuses
            .Where(s => _orderAdminService.IsValidPaymentStatusTransition(currentPaymentStatus, s, orderStatus))
            .Select(s => new { value = (int)s, text = GetPaymentStatusDisplayName(s) })
            .ToList();

        return Json(validTransitions);
    }

    // AJAX: Get transition rules for JavaScript client
    // Returns state and payment transition rules for the JavaScript client.
    [HttpGet]
    public async Task<IActionResult> GetTransitionRules(int orderId)
    {
        var order = await _orderAdminService.GetOrderByIdAsync(orderId);
        
        if (order == null)
        {
            return NotFound(new { error = "Không tìm thấy đơn hàng" });
        }

        var transitionRule = _orderAdminService.GetStateTransitionRule(order.Status);
        var combinationRule = _orderAdminService.GetStatusCombinationRule(order.Status, order.PaymentMethod);

        return Json(new
        {
            transitionRule = new
            {
                currentStatus = (int)transitionRule.CurrentStatus,
                currentStatusName = GetStatusDisplayName(transitionRule.CurrentStatus),
                allowedTransitions = transitionRule.AllowedTransitions.Select(s => new
                {
                    value = (int)s,
                    text = GetStatusDisplayName(s)
                }),
                disabledTransitions = transitionRule.DisabledTransitions.Select(s => new
                {
                    value = (int)s,
                    text = GetStatusDisplayName(s)
                })
            },
            combinationRule = new
            {
                orderStatus = (int)combinationRule.OrderStatus,
                orderStatusName = GetStatusDisplayName(combinationRule.OrderStatus),
                allowedPaymentStatuses = combinationRule.AllowedPaymentStatuses.Select(s => new
                {
                    value = (int)s,
                    text = GetPaymentStatusDisplayName(s)
                }),
                autoSetPaymentStatus = combinationRule.AutoSetPaymentStatus.HasValue
                    ? new { value = (int)combinationRule.AutoSetPaymentStatus.Value, text = GetPaymentStatusDisplayName(combinationRule.AutoSetPaymentStatus.Value) }
                    : null,
                isPaymentLocked = combinationRule.IsPaymentLocked
            },
            paymentMethod = new
            {
                value = (int)order.PaymentMethod,
                text = GetPaymentMethodDisplayName(order.PaymentMethod)
            }
        });
    }

    // AJAX: Get combination rule for a specific order status and payment method
    // Returns the payment combination rule for the JavaScript client.
    [HttpGet]
    public IActionResult GetCombinationRule(OrderStatus orderStatus, PaymentMethod paymentMethod)
    {
        var combinationRule = _orderAdminService.GetStatusCombinationRule(orderStatus, paymentMethod);

        return Json(new
        {
            orderStatus = (int)combinationRule.OrderStatus,
            orderStatusName = GetStatusDisplayName(combinationRule.OrderStatus),
            allowedPaymentStatuses = combinationRule.AllowedPaymentStatuses.Select(s => new
            {
                value = (int)s,
                text = GetPaymentStatusDisplayName(s)
            }),
            autoSetPaymentStatus = combinationRule.AutoSetPaymentStatus.HasValue
                ? new { value = (int)combinationRule.AutoSetPaymentStatus.Value, text = GetPaymentStatusDisplayName(combinationRule.AutoSetPaymentStatus.Value) }
                : null,
            isPaymentLocked = combinationRule.IsPaymentLocked
        });
    }

    private int GetCurrentAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var id) ? id : 0;
    }

    private string GetErrorMessage(OrderResult result)
    {
        return result.ErrorType switch
        {
            OrderErrorType.NotFound => "Không tìm thấy đơn hàng",
            OrderErrorType.InvalidStatusTransition => result.ErrorMessage ?? "Chuyển trạng thái không hợp lệ",
            OrderErrorType.InvalidPaymentStatusTransition => result.ErrorMessage ?? "Chuyển trạng thái thanh toán không hợp lệ",
            OrderErrorType.InsufficientStock => GetInsufficientStockMessage(result),
            OrderErrorType.ConcurrencyConflict => "Đơn hàng đã được cập nhật bởi người khác. Vui lòng tải lại trang.",
            _ => result.ErrorMessage ?? "Có lỗi xảy ra"
        };
    }

    private string GetInsufficientStockMessage(OrderResult result)
    {
        if (result.InsufficientStockItems == null || !result.InsufficientStockItems.Any())
            return "Không đủ tồn kho";

        var items = result.InsufficientStockItems
            .Select(i => $"{i.ProductName} (cần {i.RequestedQuantity}, còn {i.AvailableQuantity})")
            .ToList();

        return $"Không đủ tồn kho: {string.Join(", ", items)}";
    }

    private static string GetStatusDisplayName(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xử lý",
        OrderStatus.Processing => "Đang xử lý",
        OrderStatus.Shipped => "Đang giao",
        OrderStatus.Delivered => "Đã giao",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    private static string GetPaymentStatusDisplayName(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Chờ thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Refunded => "Đã hoàn tiền",
        _ => status.ToString()
    };

    private static string GetPaymentMethodDisplayName(PaymentMethod method) => method switch
    {
        PaymentMethod.COD => "Thanh toán khi nhận hàng (COD)",
        PaymentMethod.BankTransfer => "Chuyển khoản ngân hàng",
        PaymentMethod.Check => "Séc",
        PaymentMethod.Paypal => "PayPal",
        _ => method.ToString()
    };

    // POST: Admin/Order/5/AddNote
    [HttpPost("{id}/AddNote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, [FromBody] AddOrderNoteRequest request)
    {
        var adminId = GetCurrentAdminId();
        var adminName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Admin";

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { success = false, message = "Nội dung không được để trống" });

        var note = await _orderAdminService.AddOrderNoteAsync(id, request.Content.Trim(), adminId, adminName);

        return Ok(new
        {
            success = true,
            note = new
            {
                note.Id,
                note.AdminName,
                note.Content,
                createdAt = note.CreatedAt.AddHours(7).ToString("HH:mm dd/MM/yyyy"),
                isOwner = note.AdminId == adminId
            }
        });
    }

    // DELETE: Admin/Order/DeleteNote/5
    [HttpPost("DeleteNote/{noteId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNote(int noteId)
    {
        var adminId = GetCurrentAdminId();
        var success = await _orderAdminService.DeleteOrderNoteAsync(noteId, adminId);
        if (!success)
            return BadRequest(new { success = false, message = "Không thể xóa ghi chú này" });

        return Ok(new { success = true });
    }
}
