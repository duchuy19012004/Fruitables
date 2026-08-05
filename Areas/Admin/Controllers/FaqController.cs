using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Chat.Knowledge;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class FaqController : Controller
{
    private readonly IFaqService _faqService;

    public FaqController(IFaqService faqService)
    {
        _faqService = faqService;
    }

    // GET: Admin/Faq
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var faqs = await _faqService.GetAllAsync(ct);
        return View(new FaqListViewModel { Faqs = faqs });
    }

    // GET: Admin/Faq/Create
    public IActionResult Create()
    {
        return View(new CreateFaqViewModel());
    }

    // POST: Admin/Faq/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateFaqViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        await _faqService.CreateAsync(
            model.Title,
            model.Body,
            model.Category ?? string.Empty,
            model.IsActive,
            ct);

        TempData["Success"] = "Tạo FAQ thành công!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Faq/Edit/{id}
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var faq = await _faqService.GetByIdAsync(id, ct);
        if (faq is null)
        {
            TempData["Error"] = "Không tìm thấy FAQ";
            return RedirectToAction(nameof(Index));
        }

        var model = new EditFaqViewModel
        {
            Id = faq.Id,
            Title = faq.Title,
            Body = faq.Body,
            Category = faq.Category,
            IsActive = faq.IsActive
        };
        return View(model);
    }

    // POST: Admin/Faq/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditFaqViewModel model, CancellationToken ct)
    {
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        var updated = await _faqService.UpdateAsync(
            id,
            model.Title,
            model.Body,
            model.Category ?? string.Empty,
            model.IsActive,
            ct);

        if (updated is null)
        {
            TempData["Error"] = "Không tìm thấy FAQ";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Cập nhật FAQ thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Faq/ToggleActive/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct)
    {
        var faq = await _faqService.GetByIdAsync(id, ct);
        if (faq is null)
        {
            TempData["Error"] = "Không tìm thấy FAQ";
            return RedirectToAction(nameof(Index));
        }

        var newActive = !faq.IsActive;
        await _faqService.SetActiveAsync(id, newActive, ct);
        TempData["Success"] = newActive
            ? "Đã kích hoạt FAQ"
            : "Đã tắt FAQ";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Faq/ReindexAll
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReindexAll(CancellationToken ct)
    {
        try
        {
            await _faqService.ReindexAllAsync(ct);
            TempData["Success"] = "Đã lập chỉ mục lại toàn bộ tri thức chatbot";
        }
        catch (Exception)
        {
            TempData["Error"] = "Lỗi khi lập chỉ mục lại. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }
}
