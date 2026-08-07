using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Fruitables.Data;
using Fruitables.Models.Json;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Catalog.Combos;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Infrastructure.Json;

namespace Fruitables.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ComboController : Controller
{
    private readonly IComboService _comboService;
    private readonly IImageUploadService _imageUploadService;
    private readonly Fruitables.Repositories.Interfaces.IUnitOfWork _unitOfWork;
    private readonly ILogger<ComboController> _logger;
    private readonly ApplicationDbContext? _dbContext;
    private readonly IJsonDocumentSerializer _serializer;

    public ComboController(
        IComboService comboService,
        IImageUploadService imageUploadService,
        Fruitables.Repositories.Interfaces.IUnitOfWork unitOfWork,
        ILogger<ComboController> logger,
        ApplicationDbContext? dbContext = null,
        IJsonDocumentSerializer? serializer = null)
    {
        _comboService = comboService;
        _imageUploadService = imageUploadService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _dbContext = dbContext;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    public async Task<IActionResult> Index()
    {
        var items = await _comboService.GetAdminListAsync();
        ViewBag.ComboWarnings = await GetSentimentWarningsAsync();
        return View(new ComboListViewModel { Items = items.ToList() });
    }

    // Cảnh báo combo chứa sản phẩm có review tiêu cực nhiều (kết hợp module phân tích cảm xúc)
    private async Task<Dictionary<int, List<string>>> GetSentimentWarningsAsync()
    {
        var warnings = new Dictionary<int, List<string>>();
        try
        {
            if (_dbContext == null)
                return warnings;

            var combos = await _dbContext.Promotions.AsNoTracking()
                .Where(promotion => promotion.Type == "combo")
                .ToListAsync();
            var payloads = combos
                .Select(promotion => (Promotion: promotion, Payload: _serializer.Deserialize<ComboPayload>(promotion.PayloadJson)))
                .Where(item => !item.Payload.IsActive || item.Payload.Items.Count > 0)
                .ToList();

            var productIds = payloads.SelectMany(c => c.Payload.Items.Select(i => i.ProductId)).Distinct().ToList();
            if (productIds.Count == 0) return warnings;
            var products = await _unitOfWork.Products.Query()
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id);

            var sentiments = await (
                from r in _unitOfWork.Reviews.Query()
                join s in _unitOfWork.ReviewSentiments.Query() on r.Id equals s.ReviewId
                where productIds.Contains(r.ProductId) && !r.IsDeleted
                group s by r.ProductId into g
                select new
                {
                    ProductId = g.Key,
                    Negative = g.Count(x => x.Sentiment == Fruitables.Models.SentimentLabel.Negative),
                    Total = g.Count()
                }).ToListAsync();

            foreach (var combo in payloads)
            {
                var comboWarnings = new List<string>();
                foreach (var item in combo.Payload.Items)
                {
                    var stat = sentiments.FirstOrDefault(s => s.ProductId == item.ProductId);
                    if (stat is null) continue;
                    var rate = stat.Total == 0 ? 0 : (float)Math.Round(stat.Negative * 100f / stat.Total, 1);
                    if (stat.Negative >= 2 || rate >= 40)
                    {
                        comboWarnings.Add($"{products.GetValueOrDefault(item.ProductId)?.Name ?? $"Sản phẩm #{item.ProductId}"}: {stat.Negative} review tiêu cực ({rate.ToString("0.0")}%)");
                    }
                }
                if (comboWarnings.Count > 0)
                    warnings[combo.Promotion.Id] = comboWarnings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing combo sentiment warnings");
        }
        return warnings;
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

        model.ImageUrl = null;
        var uploadedUrl = await TryUploadComboImageAsync(model.ImageFile);
        if (uploadedUrl == null && model.ImageFile != null)
        {
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        if (!string.IsNullOrEmpty(uploadedUrl))
            model.ImageUrl = uploadedUrl;

        var result = await _comboService.CreateAsync(model, GetAdminId());
        if (!result.Success)
        {
            if (!string.IsNullOrEmpty(uploadedUrl))
                await _imageUploadService.DeleteImageAsync(uploadedUrl);

            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        TempData["Success"] = "Tạo combo sản phẩm thành công!";
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
        var existing = await _comboService.GetForEditAsync(id);
        if (existing == null)
        {
            TempData["Error"] = "Không tìm thấy combo";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            model.ImageUrl = existing.ImageUrl;
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        var oldImageUrl = existing.ImageUrl;
        string? uploadedUrl = null;
        model.ImageUrl = oldImageUrl;
        if (model.ImageFile != null)
        {
            uploadedUrl = await TryUploadComboImageAsync(model.ImageFile);
            if (uploadedUrl == null)
            {
                model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
                return View(model);
            }

            model.ImageUrl = uploadedUrl;
        }

        var result = await _comboService.UpdateAsync(id, model, GetAdminId());
        if (!result.Success)
        {
            if (!string.IsNullOrEmpty(uploadedUrl))
                await _imageUploadService.DeleteImageAsync(uploadedUrl);

            ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        if (!string.IsNullOrEmpty(uploadedUrl) && !string.IsNullOrEmpty(oldImageUrl))
            await _imageUploadService.DeleteImageAsync(oldImageUrl);

        TempData["Success"] = "Cập nhật combo sản phẩm thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _comboService.DeleteAsync(id, GetAdminId());
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Đã ngừng bán combo sản phẩm."
            : result.ErrorMessage ?? "Không thể ngừng bán combo";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Audit(int id)
    {
        var model = await _comboService.GetAuditAsync(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Report(DateTime? from = null, DateTime? to = null)
    {
        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? end.AddDays(-29)).Date;
        return View(await _comboService.GetReportAsync(start, end));
    }

    [HttpGet]
    public async Task<IActionResult> GetVariants(int productId)
    {
        var products = await _comboService.GetProductOptionsAsync();
        var product = products.FirstOrDefault(p => p.Id == productId);
        return Json(product?.Variants ?? new List<ComboVariantOptionViewModel>());
    }

    private int? GetAdminId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ? adminId : null;

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
