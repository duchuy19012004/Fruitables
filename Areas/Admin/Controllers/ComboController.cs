using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ComboController : Controller
{
    private readonly IComboService _comboService;
    private readonly IImageUploadService _imageUploadService;

    public ComboController(IComboService comboService, IImageUploadService imageUploadService)
    {
        _comboService = comboService;
        _imageUploadService = imageUploadService;
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

    private async Task<string?> TryUploadComboImageAsync(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
            return null;

        if (!_imageUploadService.IsValidImageFile(imageFile))
        {
            ModelState.AddModelError("ImageFile", "File không phải định dạng ảnh hợp lệ (.jpg, .jpeg, .png, .gif, .webp).");
            return null;
        }

        if (!_imageUploadService.IsValidFileSize(imageFile))
        {
            ModelState.AddModelError("ImageFile", "File vượt quá kích thước cho phép (5MB).");
            return null;
        }

        try
        {
            return await _imageUploadService.UploadImageAsync(imageFile, "combos");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("ImageFile", $"Không thể upload ảnh: {ex.Message}");
            return null;
        }
    }
}
