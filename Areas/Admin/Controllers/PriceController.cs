using System.Security.Claims;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class PriceController : Controller
{
    private readonly IPriceManagementService _prices;

    public PriceController(IPriceManagementService prices) => _prices = prices;

    public async Task<IActionResult> Index(string? search = null, string? filter = null, string tab = "prices",
        string? sort = null, string? dir = null, int page = 1,
        string? scheduleStatus = null, string? scheduleSearch = null, int schedulePage = 1)
    {
        var query = new PriceDashboardQuery
        {
            Tab = tab == "schedules" ? "schedules" : "prices",
            Search = search,
            Filter = filter is "active" or "upcoming" or "regular" ? filter : null,
            Sort = sort is "base" or "effective" ? sort : "name",
            Dir = dir == "desc" ? "desc" : "asc",
            Page = Math.Max(1, page),
            ScheduleStatus = scheduleStatus is "active" or "scheduled" or "ended" or "cancelled" ? scheduleStatus : null,
            ScheduleSearch = scheduleSearch,
            SchedulePage = Math.Max(1, schedulePage)
        };
        return View(await _prices.GetDashboardAsync(query));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSchedule(SavePriceScheduleRequest request)
    {
        NormalizeVietnamTime(request);
        return ResultResponse(await _prices.CreateScheduleAsync(request, CurrentAdminId()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule(int id, SavePriceScheduleRequest request)
    {
        NormalizeVietnamTime(request);
        return ResultResponse(await _prices.UpdateScheduleAsync(id, request, CurrentAdminId()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSchedule(int id) =>
        ResultResponse(await _prices.CancelScheduleAsync(id, CurrentAdminId()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBasePrice(int productId, int? productVariantId, decimal newPrice) =>
        ResultResponse(await _prices.UpdateBasePriceAsync(new PriceTargetKey(productId, productVariantId), newPrice, CurrentAdminId()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpdate(List<string>? selectedTargets, PriceAdjustmentType adjustmentType,
        PriceAdjustmentDirection direction, decimal value)
    {
        var targets = (selectedTargets ?? []).Select(ParseTarget).Where(t => t.HasValue).Select(t => t!.Value).ToList();
        var request = new BulkPriceUpdateRequest { Targets = targets, AdjustmentType = adjustmentType, Direction = direction, Value = value };
        return ResultResponse(await _prices.BulkUpdateBasePricesAsync(request, CurrentAdminId()));
    }

    private static PriceTargetKey? ParseTarget(string value)
    {
        var parts = value.Split(':');
        if (!int.TryParse(parts[0], out var productId)) return null;
        return new PriceTargetKey(productId, parts.Length > 1 && int.TryParse(parts[1], out var variantId) ? variantId : null);
    }

    private int CurrentAdminId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static void NormalizeVietnamTime(SavePriceScheduleRequest request)
    {
        var vietnamOffset = TimeSpan.FromHours(7);
        request.StartsAt = new DateTimeOffset(request.StartsAt.DateTime, vietnamOffset);
        if (request.EndsAt.HasValue)
            request.EndsAt = new DateTimeOffset(request.EndsAt.Value.DateTime, vietnamOffset);
    }

    private void SetMessage(PriceManagementResult result)
    {
        if (result.Success) TempData["Success"] = "Cập nhật giá thành công.";
        else TempData["Error"] = result.Error;
    }

    /// <summary>AJAX (fetch từ trang giá) nhận JSON {success,error}; form thường (no-JS) redirect + TempData.</summary>
    private IActionResult ResultResponse(PriceManagementResult result)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, error = result.Error });
        SetMessage(result);
        return RedirectToAction(nameof(Index));
    }
}
