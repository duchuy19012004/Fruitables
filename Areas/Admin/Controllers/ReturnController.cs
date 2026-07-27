using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Data;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ReturnController : Controller
{
    private readonly IReturnService _returns;
    private readonly IRefundService _refunds;
    private readonly IReturnEvidenceService _evidence;
    private readonly IReturnDispositionService _dispositions;
    private readonly IRbacService _rbac;
    private readonly ApplicationDbContext _db;
    public ReturnController(IReturnService returns, IRefundService refunds, IReturnEvidenceService evidence, IReturnDispositionService dispositions, IRbacService rbac, ApplicationDbContext db)
    { _returns = returns; _refunds = refunds; _evidence = evidence; _dispositions = dispositions; _rbac = rbac; _db = db; }

    [RequirePermission("returns.view")]
    public async Task<IActionResult> Index(ReturnQueueFilter filter)
    {
        ViewBag.Filter = filter;
        var query = new ReturnQueueFilter
        {
            Status = filter.Status,
            Reason = filter.Reason,
            Search = filter.Search,
            FromUtc = filter.FromUtc,
            ToUtc = filter.ToUtc?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };
        return View(await _returns.GetQueueAsync(query));
    }

    [RequirePermission("returns.view")]
    public async Task<IActionResult> Detail(int id)
    {
        var request = await _returns.GetForAdminAsync(id);
        if (request == null) return NotFound();
        var orderItemIds = request.Items.Select(x => x.OrderItemId).ToList();
        ViewBag.PriorApprovedQuantities = await _db.ReturnRequestItems.AsNoTracking()
            .Where(x => x.ReturnRequestId != id && orderItemIds.Contains(x.OrderItemId))
            .GroupBy(x => x.OrderItemId)
            .ToDictionaryAsync(x => x.Key, x => x.Sum(item => item.ApprovedQuantity));
        return View(request);
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.review")]
    public async Task<IActionResult> RequestEvidence(int id, string note, string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(note)) TempData["Error"] = "Cần nêu rõ bằng chứng cần bổ sung.";
        else SetResult(await _returns.RequestEvidenceAsync(id, AdminId, note, Decode(rowVersion)));
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.review")]
    public async Task<IActionResult> StartReview(int id, string rowVersion)
    {
        SetResult(await _returns.StartReviewAsync(id, AdminId, Decode(rowVersion)));
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.approve", "returns.reject")]
    public async Task<IActionResult> Decide(ReturnDecisionViewModel model)
    {
        var permission = model.Items.Any(x => x.ApprovedQuantity > 0) ? "returns.approve" : "returns.reject";
        if (!await _rbac.HasPermissionAsync(AdminId, permission)) return Forbid();
        SetResult(await _returns.DecideAsync(AdminId, model));
        return RedirectToAction(nameof(Detail), new { id = model.ReturnRequestId });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public async Task<IActionResult> CreateRefund(int id, int returnRequestItemId, decimal amount, RefundMethod method, string idempotencyKey)
    {
        var result = await _refunds.CreateAsync(id, returnRequestItemId, amount, method, idempotencyKey, AdminId);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã tạo khoản hoàn tiền." : result.Error;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public async Task<IActionResult> ConfirmRefund(int id, int returnRequestId, string transactionReference, IFormFile transferEvidence)
    {
        if (transferEvidence == null) TempData["Error"] = "Cần tải bằng chứng chuyển tiền.";
        else
        {
            var upload = await _evidence.UploadAsync(returnRequestId, null, AdminId, transferEvidence, true);
            if (!upload.Success) TempData["Error"] = upload.Error;
            else
            {
                var result = await _refunds.ConfirmManualAsync(id, transactionReference, upload.Evidence!.StorageKey, AdminId);
                TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã xác nhận hoàn tiền." : result.Error;
            }
        }
        return RedirectToAction(nameof(Detail), new { id = returnRequestId });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.approve")]
    public async Task<IActionResult> UpdateResolution(int id, ReturnRequestStatus target, string note, string rowVersion)
    {
        SetResult(await _returns.UpdateResolutionAsync(id, AdminId, target, note, Decode(rowVersion)));
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.review")]
    public async Task<IActionResult> RecordDisposition(int returnRequestId, int returnItemId, int quantity, InventoryDispositionType disposition, string notes)
    {
        var canOverride = await _rbac.HasPermissionAsync(AdminId, "returns.override_policy");
        var result = await _dispositions.RecordAsync(returnItemId, quantity, disposition, AdminId, notes, canOverride);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã ghi nhận disposition." : result.Error;
        return RedirectToAction(nameof(Detail), new { id = returnRequestId });
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static byte[] Decode(string value) { try { return string.IsNullOrWhiteSpace(value) ? Array.Empty<byte>() : Convert.FromBase64String(value); } catch { return Array.Empty<byte>(); } }
    private void SetResult(ReturnResult result) => TempData[result.Success ? "Success" : "Error"] = result.Success ? "Cập nhật thành công." : result.Error;
}
