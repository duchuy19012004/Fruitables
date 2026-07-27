using System.Security.Claims;
using Fruitables.Data;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Controllers;

[Authorize]
public class ReturnController : Controller
{
    private readonly IReturnService _returns;
    private readonly IReturnEligibilityService _eligibility;
    private readonly IReturnEvidenceService _evidence;
    private readonly IRefundService _refunds;
    private readonly ApplicationDbContext _db;

    public ReturnController(IReturnService returns, IReturnEligibilityService eligibility, IReturnEvidenceService evidence, IRefundService refunds, ApplicationDbContext db)
    {
        _returns = returns; _eligibility = eligibility; _evidence = evidence; _refunds = refunds; _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _returns.GetCustomerRequestsAsync(UserId));

    [HttpGet]
    public async Task<IActionResult> Create(int orderId)
    {
        var eligibility = await _eligibility.CheckOrderAsync(orderId, UserId);
        if (!eligibility.Eligible) { TempData["ErrorMessage"] = eligibility.Error; return RedirectToAction("Details", "OrderHistory", new { id = orderId }); }
        await LoadCreateDataAsync(orderId, eligibility);
        return View(new ReturnSubmitViewModel { OrderId = orderId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 41_943_040)]
    public async Task<IActionResult> Create(ReturnSubmitViewModel model)
    {
        if (!ModelState.IsValid) return await RedisplayCreate(model);
        var result = await _returns.SubmitAsync(UserId, model);
        if (!result.Success || result.Request == null) { ModelState.AddModelError(string.Empty, result.Error ?? "Không thể gửi yêu cầu."); return await RedisplayCreate(model); }
        foreach (var file in model.EvidenceFiles ?? [])
        {
            var upload = await _evidence.UploadAsync(result.Request.Id, null, UserId, file, false);
            if (!upload.Success) { TempData["ErrorMessage"] = upload.Error; return RedirectToAction(nameof(Details), new { id = result.Request.Id }); }
        }
        TempData["SuccessMessage"] = $"Đã gửi yêu cầu {result.Request.ReturnNumber}.";
        return RedirectToAction(nameof(Details), new { id = result.Request.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var request = await _returns.GetForCustomerAsync(id, UserId);
        return request == null ? NotFound() : View(request);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEvidence(int id, List<IFormFile> files)
    {
        foreach (var file in files)
        {
            var result = await _evidence.UploadAsync(id, null, UserId, file, false);
            if (!result.Success) { TempData["ErrorMessage"] = result.Error; break; }
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _returns.CancelAsync(id, UserId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success ? "Đã hủy yêu cầu." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRefundDestination(RefundDestinationInputViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin nhận tiền không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.ReturnRequestId });
        }

        var result = await _refunds.SaveDestinationAsync(model.RefundId, UserId, model);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
            ? "Đã lưu thông tin nhận tiền."
            : result.Error;
        return RedirectToAction(nameof(Details), new { id = model.ReturnRequestId });
    }

    private async Task<IActionResult> RedisplayCreate(ReturnSubmitViewModel model)
    {
        var eligibility = await _eligibility.CheckOrderAsync(model.OrderId, UserId);
        await LoadCreateDataAsync(model.OrderId, eligibility);
        return View(model);
    }

    private async Task LoadCreateDataAsync(int orderId, ReturnEligibilityResult eligibility)
    {
        var order = await _db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == UserId);
        ViewBag.Order = order;
        ViewBag.Eligibility = eligibility;
        var rules = new Dictionary<int, Dictionary<ReturnReasonCode, bool>>();
        if (order != null)
        {
            foreach (var item in order.Items)
            {
                rules[item.Id] = new Dictionary<ReturnReasonCode, bool>();
                foreach (var reason in Enum.GetValues<ReturnReasonCode>())
                {
                    var check = await _eligibility.CheckItemAsync(orderId, item.Id, UserId, reason);
                    if (check.Eligible && check.Policy != null)
                        rules[item.Id][reason] = check.EvidenceRequired;
                }
            }
        }
        ViewBag.ReasonRules = rules;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
