using System.Security.Claims;
using Fruitables.Services.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Controllers;

[Authorize]
public sealed class ReturnController : Controller
{
    private readonly IReturnService _returnService;

    public ReturnController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int orderId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");

        var eligibility = await _returnService.GetEligibilityAsync(orderId, userId.Value);
        if (!eligibility.CanCreate)
        {
            TempData["ErrorMessage"] = "Đơn hàng không đủ điều kiện gửi khiếu nại.";
            return RedirectToAction("Details", "OrderHistory", new { id = orderId });
        }

        return View(eligibility);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReturnCommand command)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");

        var result = await _returnService.CreateAsync(command, userId.Value);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Create), new { orderId = command.OrderId });
        }

        TempData["SuccessMessage"] = "Đã gửi yêu cầu khiếu nại.";
        return RedirectToAction(nameof(Details), new { id = result.ReturnRequestId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");

        var detail = await _returnService.GetCustomerDetailAsync(id, userId.Value);
        if (detail == null)
            return RedirectToAction("Index", "OrderHistory");

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");

        var result = await _returnService.CancelAsync(id, userId.Value);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] =
            result.Success ? "Đã hủy yêu cầu khiếu nại." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Supplement(int id, SupplementReturnCommand command)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return RedirectToAction("Login", "Account");

        var result = await _returnService.AddCustomerInfoAsync(
            command with { ReturnRequestId = id }, userId.Value);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] =
            result.Success ? "Đã gửi thông tin bổ sung." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
