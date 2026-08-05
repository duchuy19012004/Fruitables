using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Services.Returns;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class ReturnController : Controller
{
    private readonly IReturnService _returnService;

    public ReturnController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    [HttpGet]
    [RequirePermission("orders.refund")]
    public async Task<IActionResult> Index(ReturnQueueFilter filter)
    {
        var result = await _returnService.GetAdminQueueAsync(filter);
        ViewBag.Filter = filter;
        return View(result);
    }

    [HttpGet]
    [RequirePermission("orders.refund")]
    public async Task<IActionResult> Detail(int id)
    {
        var detail = await _returnService.GetAdminDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = "Không tìm thấy yêu cầu khiếu nại.";
            return RedirectToAction(nameof(Index));
        }

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("orders.refund")]
    public async Task<IActionResult> RequestInfo(RequestCustomerInfoCommand command)
    {
        var result = await _returnService.RequestCustomerInfoAsync(command, GetCurrentAdminId());
        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Đã yêu cầu khách bổ sung thông tin." : result.ErrorMessage;
        return RedirectToAction(nameof(Detail), new { id = command.ReturnRequestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("orders.refund")]
    public async Task<IActionResult> Decide(DecideReturnCommand command)
    {
        var result = await _returnService.DecideAsync(command, GetCurrentAdminId());
        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Đã lưu quyết định khiếu nại." : result.ErrorMessage;
        return RedirectToAction(nameof(Detail), new { id = command.ReturnRequestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("orders.refund")]
    public async Task<IActionResult> CompleteRefund(CompleteRefundCommand command)
    {
        var result = await _returnService.CompleteRefundAsync(command, GetCurrentAdminId());
        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Đã cập nhật kết quả hoàn tiền." : result.ErrorMessage;
        return RedirectToAction(nameof(Detail), new { id = command.ReturnRequestId });
    }

    private int GetCurrentAdminId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var adminId) ? adminId : 0;
    }
}
