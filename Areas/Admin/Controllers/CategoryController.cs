using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Catalog.Categories;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class CategoryController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    // GET: Admin/Category
    public async Task<IActionResult> Index()
    {
        var tree = await _categoryService.GetCategoryTreeAsync();
        var viewModel = new CategoryListViewModel { Categories = tree };
        return View(viewModel);
    }

    // GET: Admin/Category/Create
    public async Task<IActionResult> Create(int? parentId = null)
    {
        var tree = await _categoryService.GetCategoryTreeAsync();
        var viewModel = new CreateCategoryViewModel
        {
            ParentId = parentId,
            ParentCategories = tree
        };
        return View(viewModel);
    }

    // POST: Admin/Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCategoryViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ParentCategories = await _categoryService.GetCategoryTreeAsync();
            return View(model);
        }

        var request = new CreateCategoryRequest
        {
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            Image = model.Image,
            ParentId = model.ParentId,
            IsActive = model.IsActive
        };

        var result = await _categoryService.CreateCategoryAsync(request);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.ParentCategories = await _categoryService.GetCategoryTreeAsync();
            return View(model);
        }

        TempData["Success"] = "Tạo danh mục thành công!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Category/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category == null)
        {
            TempData["Error"] = "Không tìm thấy danh mục";
            return RedirectToAction(nameof(Index));
        }

        var tree = await _categoryService.GetCategoryTreeAsync();
        var viewModel = new EditCategoryViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            Image = category.Image,
            ParentId = category.ParentId,
            IsActive = category.IsActive,
            ParentCategories = tree
        };
        return View(viewModel);
    }

    // POST: Admin/Category/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditCategoryViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            model.ParentCategories = await _categoryService.GetCategoryTreeAsync();
            return View(model);
        }

        var request = new UpdateCategoryRequest
        {
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            Image = model.Image,
            ParentId = model.ParentId,
            IsActive = model.IsActive
        };

        var result = await _categoryService.UpdateCategoryAsync(id, request);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.ParentCategories = await _categoryService.GetCategoryTreeAsync();
            return View(model);
        }

        TempData["Success"] = "Cập nhật danh mục thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Category/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _categoryService.DeleteCategoryAsync(id);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Xóa danh mục thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Category/Reorder (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([FromBody] ReorderCategoriesRequest request)
    {
        var result = await _categoryService.ReorderCategoriesAsync(request.ParentId, request.CategoryIds);

        if (!result.Success)
            return Json(new { success = false, message = result.ErrorMessage });

        return Json(new { success = true });
    }

    // POST: Admin/Category/Move (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move([FromBody] MoveCategoryRequest request)
    {
        var result = await _categoryService.MoveCategoryAsync(request.CategoryId, request.NewParentId);

        if (!result.Success)
            return Json(new { success = false, message = result.ErrorMessage });

        return Json(new { success = true });
    }

    // GET: Admin/Category/Trash
    public async Task<IActionResult> Trash()
    {
        var deletedCategories = await _categoryService.GetDeletedCategoriesAsync();
        return View(deletedCategories);
    }

    // POST: Admin/Category/SoftDelete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(int id)
    {
        var result = await _categoryService.SoftDeleteCategoryAsync(id);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Đã chuyển danh mục vào thùng rác!";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Category/Restore/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var result = await _categoryService.RestoreCategoryAsync(id);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Trash));
        }

        TempData["Success"] = "Đã khôi phục danh mục thành công!";
        return RedirectToAction(nameof(Trash));
    }
}
