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

    public async Task<IActionResult> Index(string? search, string? filter) => View(await _prices.GetDashboardAsync(search, filter));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSchedule(SavePriceScheduleRequest request)
    {
        NormalizeVietnamTime(request);
        SetMessage(await _prices.CreateScheduleAsync(request, CurrentAdminId()));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule(int id, SavePriceScheduleRequest request)
    {
        NormalizeVietnamTime(request);
        SetMessage(await _prices.UpdateScheduleAsync(id, request, CurrentAdminId()));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSchedule(int id)
    {
        SetMessage(await _prices.CancelScheduleAsync(id, CurrentAdminId()));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBasePrice(int productId, int? productVariantId, decimal newPrice)
    {
        SetMessage(await _prices.UpdateBasePriceAsync(new PriceTargetKey(productId, productVariantId), newPrice, CurrentAdminId()));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpdate(List<string>? selectedTargets, PriceAdjustmentType adjustmentType,
        PriceAdjustmentDirection direction, decimal value)
    {
        var targets = (selectedTargets ?? []).Select(ParseTarget).Where(t => t.HasValue).Select(t => t!.Value).ToList();
        var request = new BulkPriceUpdateRequest { Targets = targets, AdjustmentType = adjustmentType, Direction = direction, Value = value };
        SetMessage(await _prices.BulkUpdateBasePricesAsync(request, CurrentAdminId()));
        return RedirectToAction(nameof(Index));
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
}
