using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ComboController : Controller
{
    private readonly IComboService _comboService;

    public ComboController(IComboService comboService)
    {
        _comboService = comboService;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _comboService.GetAdminListAsync();
        return View(new ComboListViewModel { Items = items.ToList() });
    }

    public async Task<IActionResult> Create()
    {
        var products = await _comboService.GetProductOptionsAsync();
        return View(new CreateComboViewModel { Products = products.ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ComboFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        var result = await _comboService.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        TempData["Success"] = "Tạo combo món ăn thành công!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var model = await _comboService.GetForEditAsync(id);
        if (model == null)
        {
            TempData["Error"] = "Không tìm thấy combo";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ComboFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        var result = await _comboService.UpdateAsync(id, model);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        TempData["Success"] = "Cập nhật combo món ăn thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _comboService.DeleteAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Xóa combo món ăn thành công!"
            : result.ErrorMessage ?? "Không thể xóa combo";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetVariants(int productId)
    {
        var products = await _comboService.GetProductOptionsAsync();
        var product = products.FirstOrDefault(p => p.Id == productId);
        return Json(product?.Variants ?? new List<ComboVariantOptionViewModel>());
    }
}
