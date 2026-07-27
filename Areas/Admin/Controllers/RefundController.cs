using System.Security.Claims;
using Fruitables.Attributes;
using Fruitables.Data;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class RefundController : Controller
{
    private const string SuccessMessage = "Cập nhật thành công.";
    private const string ErrorMessage = "Không thể cập nhật khoản hoàn.";

    private readonly IRefundService _refunds;
    private readonly IReturnEvidenceService _evidence;
    private readonly ApplicationDbContext _db;

    public RefundController(IRefundService refunds, IReturnEvidenceService evidence, ApplicationDbContext db)
    {
        _refunds = refunds;
        _evidence = evidence;
        _db = db;
    }

    [RequirePermission("returns.refund")]
    public async Task<IActionResult> Index(RefundQueueFilter filter)
    {
        ViewBag.Filter = filter;
        return View(await _refunds.GetQueueAsync(filter));
    }

    [RequirePermission("returns.refund")]
    public async Task<IActionResult> Detail(int id)
    {
        var task = await _refunds.GetFinanceTaskAsync(id, AdminId);
        if (!task.Success || task.Data == null)
            return NotFound();

        var detail = await _db.Refunds.AsNoTracking()
            .Where(x => x.Id == id && x.ReturnRequestItemId == null)
            .Select(x => new
            {
                x.FailureReason,
                x.TransactionReference,
                x.ProcessedAtUtc,
                x.TransferEvidenceStorageKey,
                TransferEvidenceId = _db.ReturnEvidences
                    .Where(e => e.ReturnRequestId == x.ReturnRequestId && e.StorageKey == x.TransferEvidenceStorageKey)
                    .Select(e => (int?)e.Id)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync();
        if (detail == null)
            return NotFound();

        ViewBag.FailureReason = detail.FailureReason;
        ViewBag.TransactionReference = detail.TransactionReference;
        ViewBag.ProcessedAtUtc = detail.ProcessedAtUtc;
        ViewBag.TransferEvidenceId = detail.TransferEvidenceId;
        return View(task.Data);
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public async Task<IActionResult> Start(int id)
    {
        SetResult(await _refunds.StartProcessingAsync(id, AdminId));
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public async Task<IActionResult> Fail(RefundFailureInputViewModel model)
    {
        SetResult(await _refunds.FailAsync(model.RefundId, AdminId, model));
        return RedirectToAction(nameof(Detail), new { id = model.RefundId });
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission("returns.refund")]
    public async Task<IActionResult> Confirm(int id, string transactionReference, IFormFile transferEvidence)
    {
        var task = await _refunds.GetFinanceTaskAsync(id, AdminId);
        if (!task.Success || task.Data == null)
            return NotFound();

        if (transferEvidence == null || transferEvidence.Length <= 0)
        {
            TempData["Error"] = ErrorMessage;
            return RedirectToAction(nameof(Detail), new { id });
        }

        var upload = await _evidence.UploadAsync(task.Data.ReturnRequestId, null, AdminId, transferEvidence, true);
        if (!upload.Success || upload.Evidence == null)
        {
            TempData["Error"] = ErrorMessage;
            return RedirectToAction(nameof(Detail), new { id });
        }

        SetResult(await _refunds.ConfirmManualAsync(id, transactionReference, upload.Evidence.StorageKey, AdminId));
        return RedirectToAction(nameof(Detail), new { id });
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private void SetResult((bool Success, string? Error) result) => TempData[result.Success ? "Success" : "Error"] = result.Success ? SuccessMessage : ErrorMessage;
}
