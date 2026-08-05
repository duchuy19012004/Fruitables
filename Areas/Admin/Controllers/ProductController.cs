using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Catalog.Products;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ProductController : Controller
{
    private readonly IProductAdminService _productAdminService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _notifier;

    public ProductController(IProductAdminService productAdminService, IUnitOfWork unitOfWork, IRealtimeNotifier notifier)
    {
        _productAdminService = productAdminService;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    // GET: Admin/Product
    public async Task<IActionResult> Index(string? search, int? categoryId, string? sortBy, int page = 1)
    {
        var request = new ProductListRequest
        {
            Search = search,
            CategoryId = categoryId,
            SortBy = sortBy,
            Page = page,
            PageSize = 10,
            IncludeDeleted = false
        };

        var result = await _productAdminService.GetProductsAsync(request);
        var categories = await _unitOfWork.Categories.GetAllAsync();

        // Badge cảm xúc: tóm tắt review theo cảm xúc cho từng sản phẩm trong trang
        ViewBag.Sentiments = await _productAdminService.GetSentimentSummariesAsync(result.Products.Select(p => p.Id).ToList());

        var viewModel = new ProductListViewModel
        {
            Products = result.Products,
            Categories = categories.ToList(),
            Search = search,
            CategoryId = categoryId,
            SortBy = sortBy,
            CurrentPage = result.CurrentPage,
            TotalPages = result.TotalPages,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems
        };

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_ProductList", viewModel);
        }

        return View(viewModel);
    }

    // GET: Admin/Product/Create
    public async Task<IActionResult> Create()
    {
        var categories = await _unitOfWork.Categories.GetAllAsync();
        var viewModel = new CreateProductViewModel
        {
            Categories = categories.Where(c => !c.IsDeleted).ToList()
        };
        return View(viewModel);
    }

    // POST: Admin/Product/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductViewModel model, List<IFormFile>? images)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = (await _unitOfWork.Categories.GetAllAsync()).Where(c => !c.IsDeleted).ToList();
            return View(model);
        }

        var result = await _productAdminService.CreateProductAsync(model.Product);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.Categories = (await _unitOfWork.Categories.GetAllAsync()).Where(c => !c.IsDeleted).ToList();
            return View(model);
        }

        // Upload images if provided
        if (images != null && images.Any() && result.Product != null)
        {
            var imageResult = await _productAdminService.AddImagesAsync(result.Product.Id, images);
            if (!imageResult.Success)
            {
                TempData["Error"] = $"Sản phẩm đã được tạo nhưng ảnh chưa được lưu: {imageResult.ErrorMessage}";
                return RedirectToAction(nameof(Edit), new { id = result.Product.Id });
            }
        }

        TempData["Success"] = "Tạo sản phẩm thành công!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Product/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productAdminService.GetProductByIdAsync(id);
        if (product == null)
        {
            TempData["Error"] = "Không tìm thấy sản phẩm";
            return RedirectToAction(nameof(Index));
        }

        var categories = await _unitOfWork.Categories.GetAllAsync();
        var allProductTags = await _unitOfWork.ProductTags.GetAllAsync();

        var viewModel = new EditProductViewModel
        {
            Product = product,
            Categories = categories.Where(c => !c.IsDeleted).ToList(),
            AllTags = allProductTags.ToList()
        };
        return View(viewModel);
    }

    // POST: Admin/Product/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateProductRequest request, List<IFormFile>? images)
    {
        if (id != request.Id)
            return BadRequest();

        var existingProduct = await _productAdminService.GetProductByIdAsync(id);
        if (existingProduct == null)
        {
            TempData["Error"] = "Không tìm thấy sản phẩm";
            return RedirectToAction(nameof(Index));
        }
        decimal oldStock = existingProduct.StockQuantity;

        if (!ModelState.IsValid)
        {

            var categories = await _unitOfWork.Categories.GetAllAsync();
            var allProductTags = await _unitOfWork.ProductTags.GetAllAsync();

            var viewModel = new EditProductViewModel
            {
                Product = existingProduct,
                Categories = categories.Where(c => !c.IsDeleted).ToList(),
                AllTags = allProductTags.ToList()
            };
            return View(viewModel);
        }

        var result = await _productAdminService.UpdateProductAsync(request);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            var product = await _productAdminService.GetProductByIdAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _unitOfWork.Categories.GetAllAsync();
            var allProductTags = await _unitOfWork.ProductTags.GetAllAsync();

            var viewModel = new EditProductViewModel
            {
                Product = product,
                Categories = categories.Where(c => !c.IsDeleted).ToList(),
                AllTags = allProductTags.ToList()
            };
            return View(viewModel);
        }

        // Upload additional images if provided
        if (images != null && images.Any())
        {
            var imageResult = await _productAdminService.AddImagesAsync(id, images);
            if (!imageResult.Success)
            {
                TempData["Error"] = imageResult.ErrorMessage;
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        if (oldStock != request.StockQuantity)
        {
            await _notifier.NotifyStockChangedAsync(id, request.StockQuantity);
        }

        TempData["Success"] = "Cập nhật sản phẩm thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Product/SoftDelete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(int id)
    {
        var result = await _productAdminService.SoftDeleteProductAsync(id);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Đã chuyển sản phẩm vào thùng rác!";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Product/Trash
    public async Task<IActionResult> Trash(int page = 1)
    {
        var request = new ProductListRequest
        {
            Page = page,
            PageSize = 10,
            IncludeDeleted = true
        };

        var result = await _productAdminService.GetProductsAsync(request);
        var deletedProducts = result.Products.Where(p => p.IsDeleted).ToList();

        var viewModel = new ProductListViewModel
        {
            Products = deletedProducts,
            CurrentPage = result.CurrentPage,
            TotalPages = result.TotalPages,
            PageSize = result.PageSize,
            TotalItems = deletedProducts.Count
        };

        return View(viewModel);
    }

    // POST: Admin/Product/Restore/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var result = await _productAdminService.RestoreProductAsync(id);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Trash));
        }

        TempData["Success"] = "Đã khôi phục sản phẩm thành công!";
        return RedirectToAction(nameof(Trash));
    }

    // POST: Admin/Product/HardDelete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(int id)
    {
        var result = await _productAdminService.HardDeleteProductAsync(id);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Đã xóa vĩnh viễn sản phẩm!";
        return RedirectToAction(nameof(Index));
    }

    #region Image Management

    // POST: Admin/Product/UploadImages/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImages(int id, List<IFormFile> images)
    {
        if (images == null || !images.Any())
        {
            return Json(new { success = false, message = "Không có ảnh nào được chọn" });
        }

        var result = await _productAdminService.AddImagesAsync(id, images);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true, message = "Upload ảnh thành công!" });
    }

    // POST: Admin/Product/SetPrimaryImage
    [HttpPost]
    public async Task<IActionResult> SetPrimaryImage([FromBody] SetPrimaryImageRequest request)
    {
        var result = await _productAdminService.SetPrimaryImageAsync(request.ProductId, request.ImageId);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    // POST: Admin/Product/DeleteImage
    [HttpPost]
    public async Task<IActionResult> DeleteImage([FromBody] DeleteImageRequest request)
    {
        var result = await _productAdminService.DeleteImageAsync(request.ProductId, request.ImageId);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    // POST: Admin/Product/ReorderImages
    [HttpPost]
    public async Task<IActionResult> ReorderImages([FromBody] ReorderImagesRequest request)
    {
        var result = await _productAdminService.ReorderImagesAsync(request.ProductId, request.ImageIds);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    #endregion

    #region Tag Management

    // POST: Admin/Product/UpdateTags
    [HttpPost]
    public async Task<IActionResult> UpdateTags([FromBody] UpdateTagsRequest request)
    {
        var result = await _productAdminService.UpdateTagsAsync(request.ProductId, request.TagNames);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    #endregion

    #region Variant Management

    // POST: Admin/Product/AddVariant
    [HttpPost]
    public async Task<IActionResult> AddVariant([FromBody] CreateVariantRequest request)
    {
        var result = await _productAdminService.AddVariantAsync(request);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true, variant = result.Product?.Variants?.LastOrDefault() });
    }

    // POST: Admin/Product/UpdateVariant/{id}
    [HttpPost]
    public async Task<IActionResult> UpdateVariant(int id, [FromBody] UpdateVariantRequest request)
    {
        var result = await _productAdminService.UpdateVariantAsync(id, request);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    // POST: Admin/Product/DeleteVariant/{id}
    [HttpPost]
    public async Task<IActionResult> DeleteVariant(int id)
    {
        var result = await _productAdminService.DeleteVariantAsync(id);

        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    #endregion
}

#region Request Models for AJAX

public class SetPrimaryImageRequest
{
    public int ProductId { get; set; }
    public int ImageId { get; set; }
}

public class DeleteImageRequest
{
    public int ProductId { get; set; }
    public int ImageId { get; set; }
}

public class ReorderImagesRequest
{
    public int ProductId { get; set; }
    public List<int> ImageIds { get; set; } = new();
}

public class UpdateTagsRequest
{
    public int ProductId { get; set; }
    public List<string> TagNames { get; set; } = new();
}

#endregion
