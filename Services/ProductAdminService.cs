using System.Text.RegularExpressions;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fruitables.Services;

public class ProductAdminService : IProductAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageUploadService _imageUploadService;
    private readonly IIndexingService _indexing;
    private readonly ILogger<ProductAdminService> _logger;
    private readonly IRealtimeNotifier? _notifier;

    public ProductAdminService(
        IUnitOfWork unitOfWork,
        IImageUploadService imageUploadService,
        IIndexingService indexing,
        ILogger<ProductAdminService> logger,
        IRealtimeNotifier? notifier = null)
    {
        _unitOfWork = unitOfWork;
        _imageUploadService = imageUploadService;
        _indexing = indexing;
        _logger = logger;
        _notifier = notifier;
    }

    private async Task TryIndexProductAsync(int productId)
    {
        try
        {
            await _indexing.IndexProductAsync(productId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index product {Id} failed", productId);
        }
    }

    #region Helper Methods

    private static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var slug = name.ToLowerInvariant();
        
        slug = slug.Replace("đ", "d").Replace("Đ", "d");
        slug = Regex.Replace(slug, "[àáạảãâầấậẩẫăằắặẳẵ]", "a");
        slug = Regex.Replace(slug, "[èéẹẻẽêềếệểễ]", "e");
        slug = Regex.Replace(slug, "[ìíịỉĩ]", "i");
        slug = Regex.Replace(slug, "[òóọỏõôồốộổỗơờớợởỡ]", "o");
        slug = Regex.Replace(slug, "[ùúụủũưừứựửữ]", "u");
        slug = Regex.Replace(slug, "[ỳýỵỷỹ]", "y");
        
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        // If slug is empty after processing, generate a fallback
        if (string.IsNullOrEmpty(slug))
        {
            slug = "product-" + Guid.NewGuid().ToString("N")[..8];
        }

        return slug;
    }

    #endregion

    #region Product CRUD

    public async Task<ProductListResult> GetProductsAsync(ProductListRequest request)
    {
        var query = _unitOfWork.Products.Query();

        // Filter by deleted status
        if (!request.IncludeDeleted)
        {
            query = query.Where(p => !p.IsDeleted);
        }

        // Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchTerm) || 
                                    (p.Description != null && p.Description.ToLower().Contains(searchTerm)));
        }

        // Filter by category
        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        // Sorting
        query = request.SortBy?.ToLower() switch
        {
            "name" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            "price" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "created" => query.OrderBy(p => p.CreatedAt),
            "created_desc" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        // Get total count
        var totalCount = await query.CountAsync();

        // Pagination
        var products = await query
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new ProductListResult
        {
            Products = products,
            TotalItems = totalCount,
            CurrentPage = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Tags)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);
        
        return product;
    }

    public async Task<ProductResult> CreateProductAsync(CreateProductRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Tên sản phẩm không được để trống");

        if (request.Price <= 0)
            return ProductResult.Fail(ProductErrorType.ValidationError, "Giá phải lớn hơn 0");

        // Generate slug if not provided
        var slug = string.IsNullOrWhiteSpace(request.Slug) 
            ? GenerateSlug(request.Name) 
            : request.Slug;

        // Check duplicate slug
        var existingProducts = await _unitOfWork.Products
            .FindAsync(p => p.Slug == slug);
        if (existingProducts.Any())
            return ProductResult.Fail(ProductErrorType.DuplicateSlug, $"Slug '{slug}' đã tồn tại");

        // Check category exists
        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
        if (category == null)
            return ProductResult.Fail(ProductErrorType.InvalidCategory, $"Danh mục với ID {request.CategoryId} không tồn tại");

        var product = new Product
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description,
            ShortDescription = request.ShortDescription,
            CategoryId = request.CategoryId,
            Price = request.Price,
            Unit = request.Unit,
            Weight = request.Weight,
            CountryOrigin = request.CountryOrigin,
            Quality = request.Quality,
            StockQuantity = request.StockQuantity,
            MinOrderQuantity = request.MinOrderQuantity,
            IsFeatured = request.IsFeatured,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        await TryIndexProductAsync(product.Id);

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> UpdateProductAsync(UpdateProductRequest request)
    {
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == request.Id);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {request.Id}");

        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Tên sản phẩm không được để trống");

        // Generate slug if not provided
        var slug = string.IsNullOrWhiteSpace(request.Slug) 
            ? GenerateSlug(request.Name) 
            : request.Slug;

        // Check duplicate slug (excluding current product)
        var existingProducts = await _unitOfWork.Products
            .FindAsync(p => p.Slug == slug && p.Id != request.Id);
        if (existingProducts.Any())
            return ProductResult.Fail(ProductErrorType.DuplicateSlug, $"Slug '{slug}' đã tồn tại");

        // Check category exists
        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId);
        if (category == null)
            return ProductResult.Fail(ProductErrorType.InvalidCategory, $"Danh mục với ID {request.CategoryId} không tồn tại");

        var oldStock = product.StockQuantity;
        product.Name = request.Name.Trim();
        product.Slug = slug;
        product.Description = request.Description;
        product.ShortDescription = request.ShortDescription;
        product.CategoryId = request.CategoryId;
        product.Unit = request.Unit;
        product.Weight = request.Weight;
        product.CountryOrigin = request.CountryOrigin;
        product.Quality = request.Quality;
        product.StockQuantity = request.StockQuantity;
        product.MinOrderQuantity = request.MinOrderQuantity;
        product.IsFeatured = request.IsFeatured;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        await TryIndexProductAsync(product.Id);
        if (_notifier != null && oldStock != product.StockQuantity &&
            !await _unitOfWork.ProductVariants.AnyAsync(variant => variant.ProductId == product.Id && variant.IsActive))
            await _notifier.NotifyStockChangedAsync(product.Id, product.StockQuantity);

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> SoftDeleteProductAsync(int id)
    {
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == id);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {id}");

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        await TryIndexProductAsync(product.Id);

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> RestoreProductAsync(int id)
    {
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == id);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {id}");

        product.IsDeleted = false;
        product.DeletedAt = null;
        product.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        await TryIndexProductAsync(product.Id);

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> HardDeleteProductAsync(int id)
    {
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == id);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {id}");

        // Check if product is in any orders
        var hasOrderItems = await _unitOfWork.OrderItems
            .AnyAsync(oi => oi.ProductId == id);
        
        if (hasOrderItems)
        {
            return ProductResult.Fail(ProductErrorType.HasOrders, 
                "Không thể xóa vĩnh viễn sản phẩm đã có trong đơn hàng. Hãy sử dụng chức năng xóa mềm.");
        }
        if (await _unitOfWork.PriceSchedules.AnyAsync(schedule => schedule.ProductId == id))
            return ProductResult.Fail(ProductErrorType.ValidationError,
                "Không thể xóa vĩnh viễn sản phẩm đã có lịch sử giá. Hãy sử dụng chức năng xóa mềm.");
        if (await _unitOfWork.CartItems.AnyAsync(item => item.ProductId == id))
            return ProductResult.Fail(ProductErrorType.ValidationError,
                "Không thể xóa vĩnh viễn sản phẩm đang nằm trong giỏ hàng. Hãy sử dụng chức năng xóa mềm.");

        // Delete related data
        var images = await _unitOfWork.ProductImages
            .FindAsync(pi => pi.ProductId == id);
        foreach (var image in images)
        {
            _unitOfWork.ProductImages.Remove(image);
        }

        // Clear tags (many-to-many relationship)
        product.Tags.Clear();

        var variants = await _unitOfWork.ProductVariants
            .FindAsync(pv => pv.ProductId == id);
        foreach (var variant in variants)
        {
            _unitOfWork.ProductVariants.Remove(variant);
        }

        var productId = product.Id;
        _unitOfWork.Products.Remove(product);
        await _unitOfWork.SaveChangesAsync();

        // Missing product deactivates knowledge chunks in IndexingService.
        await TryIndexProductAsync(productId);

        return ProductResult.Ok(product);
    }

    #endregion

    #region Image Management

    public async Task<ProductResult> AddImagesAsync(int productId, List<IFormFile> files)
    {
        // Check product exists
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == productId);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

        // Validate files
        foreach (var file in files)
        {
            if (!_imageUploadService.IsValidImageFile(file))
                return ProductResult.Fail(ProductErrorType.InvalidFileType, "File không phải định dạng ảnh hợp lệ");

            if (!_imageUploadService.IsValidFileSize(file))
                return ProductResult.Fail(ProductErrorType.FileTooLarge, "File vượt quá kích thước cho phép (5MB)");
        }

        // Get current max sort order
        var existingImages = await _unitOfWork.ProductImages
            .FindAsync(pi => pi.ProductId == productId);
        var maxSortOrder = existingImages.Any() ? existingImages.Max(i => i.SortOrder) : -1;

        // Upload and save images
        foreach (var file in files)
        {
            var imageUrl = await _imageUploadService.UploadImageAsync(file, "products");
            
            var productImage = new ProductImage
            {
                ProductId = productId,
                ImageUrl = imageUrl,
                IsPrimary = !existingImages.Any() && maxSortOrder == -1, // First image is primary
                SortOrder = ++maxSortOrder
            };

            await _unitOfWork.ProductImages.AddAsync(productImage);
        }

        await _unitOfWork.SaveChangesAsync();

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> SetPrimaryImageAsync(int productId, int imageId)
    {
        // Check product exists
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == productId);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

        // Get all images for this product
        var images = await _unitOfWork.ProductImages
            .FindAsync(pi => pi.ProductId == productId);

        // Check if target image exists
        var targetImage = images.FirstOrDefault(i => i.Id == imageId);
        if (targetImage == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy ảnh với ID {imageId}");

        // Reset all images to non-primary
        foreach (var image in images)
        {
            image.IsPrimary = false;
        }

        // Set target image as primary
        targetImage.IsPrimary = true;

        await _unitOfWork.SaveChangesAsync();

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> DeleteImageAsync(int productId, int imageId)
    {
        // Check product exists
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == productId);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

        // Get image
        var images = await _unitOfWork.ProductImages
            .FindAsync(pi => pi.Id == imageId && pi.ProductId == productId);
        var image = images.FirstOrDefault();

        if (image == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy ảnh với ID {imageId}");

        // Delete physical file
        await _imageUploadService.DeleteImageAsync(image.ImageUrl);

        // Delete database record
        _unitOfWork.ProductImages.Remove(image);
        await _unitOfWork.SaveChangesAsync();

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> ReorderImagesAsync(int productId, List<int> imageIds)
    {
        // Check product exists
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == productId);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

        // Get all images for this product
        var images = await _unitOfWork.ProductImages
            .FindAsync(pi => pi.ProductId == productId);

        // Update sort order based on new order
        for (int i = 0; i < imageIds.Count; i++)
        {
            var image = images.FirstOrDefault(img => img.Id == imageIds[i]);
            if (image != null)
            {
                image.SortOrder = i;
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return ProductResult.Ok(product);
    }

    #endregion

    #region Tag Management

    public async Task<ProductResult> UpdateTagsAsync(int productId, List<string> tagNames)
    {
        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

        // Validate tag names
        if (tagNames.Any(string.IsNullOrWhiteSpace))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Tên tag không được để trống");

        // Remove duplicates and trim
        tagNames = tagNames
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Clear existing tags for this product
        product.Tags.Clear();

        // Query all existing tags at once
        var lowercaseTagNames = tagNames.Select(t => t.ToLowerInvariant()).ToList();
        var existingTags = await _unitOfWork.ProductTags.Query()
            .Where(t => lowercaseTagNames.Contains(t.Name.ToLower()))
            .ToListAsync();

        var existingTagsDict = existingTags
            .GroupBy(t => t.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        // Add new tags
        foreach (var tagName in tagNames)
        {
            var key = tagName.ToLowerInvariant();
            if (!existingTagsDict.TryGetValue(key, out var tag))
            {
                // Create new tag
                tag = new ProductTag
                {
                    Name = tagName,
                    Slug = GenerateSlug(tagName)
                };
                await _unitOfWork.ProductTags.AddAsync(tag);
                existingTagsDict[key] = tag;
            }

            // Add tag to product
            product.Tags.Add(tag);
        }

        await _unitOfWork.SaveChangesAsync();

        return ProductResult.Ok(product);
    }

    #endregion

    #region Variant Management

    public async Task<ProductResult> AddVariantAsync(CreateVariantRequest request)
    {
        await using var transaction = await BeginVariantWriteAsync();
        // Check product exists
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == request.ProductId);
        var product = products.FirstOrDefault();

        if (product == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {request.ProductId}");

        if (request.IsActive && !await CanEnableVariantAsync(request.ProductId))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Hãy hủy các lịch giá cấp sản phẩm đang chạy hoặc sắp tới trước khi kích hoạt biến thể.");

        // Validation
        if (request.Price <= 0)
            return ProductResult.Fail(ProductErrorType.ValidationError, "Giá phải lớn hơn 0");

        if (request.StockQuantity < 0)
            return ProductResult.Fail(ProductErrorType.ValidationError, "Số lượng tồn kho không được âm");

        // Check duplicate SKU
        var existingVariants = await _unitOfWork.ProductVariants
            .FindAsync(pv => pv.SKU == request.SKU);
        if (existingVariants.Any())
            return ProductResult.Fail(ProductErrorType.DuplicateSKU, $"SKU '{request.SKU}' đã tồn tại");

        var variant = new ProductVariant
        {
            ProductId = request.ProductId,
            SKU = request.SKU,
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            IsActive = request.IsActive
        };

        await _unitOfWork.ProductVariants.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        if (_notifier != null && variant.IsActive)
        {
            await _notifier.NotifyStockChangedAsync(product.Id, variant.StockQuantity, variant.Id);
            await _notifier.NotifyPriceChangedAsync(product.Id, variant.Id);
        }
        await TryIndexProductAsync(product.Id);

        return ProductResult.Ok(product);
    }

    public async Task<ProductResult> UpdateVariantAsync(int variantId, UpdateVariantRequest request)
    {
        await using var transaction = await BeginVariantWriteAsync();
        var variants = await _unitOfWork.ProductVariants
            .FindAsync(pv => pv.Id == variantId);
        var variant = variants.FirstOrDefault();

        if (variant == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy variant với ID {variantId}");

        if (request.IsActive && !variant.IsActive && !await CanEnableVariantAsync(variant.ProductId))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Hãy hủy các lịch giá cấp sản phẩm đang chạy hoặc sắp tới trước khi kích hoạt biến thể.");

        // Validation
        if (request.StockQuantity < 0)
            return ProductResult.Fail(ProductErrorType.ValidationError, "Số lượng tồn kho không được âm");

        // Check duplicate SKU (excluding current variant)
        var existingVariants = await _unitOfWork.ProductVariants
            .FindAsync(pv => pv.SKU == request.SKU && pv.Id != variantId);
        if (existingVariants.Any())
            return ProductResult.Fail(ProductErrorType.DuplicateSKU, $"SKU '{request.SKU}' đã tồn tại");

        var oldStock = variant.StockQuantity;
        var wasActive = variant.IsActive;

        if (variant.IsActive && !request.IsActive)
        {
            var transitionError = await PrepareLastVariantDeactivationAsync(variant);
            if (transitionError != null)
                return ProductResult.Fail(ProductErrorType.ValidationError, transitionError);
        }

        variant.SKU = request.SKU;
        variant.Name = request.Name;
        variant.StockQuantity = request.StockQuantity;
        variant.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync();

        // Get product for result
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == variant.ProductId);
        var product = products.FirstOrDefault();
        if (transaction != null) await transaction.CommitAsync();
        if (_notifier != null && product != null && (oldStock != variant.StockQuantity || wasActive != variant.IsActive))
        {
            await _notifier.NotifyStockChangedAsync(product.Id, variant.StockQuantity, variant.Id);
            if (wasActive != variant.IsActive) await _notifier.NotifyPriceChangedAsync(product.Id, variant.Id);
        }
        if (product != null) await TryIndexProductAsync(product.Id);

        return product != null 
            ? ProductResult.Ok(product) 
            : ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {variant.ProductId}");
    }

    public async Task<ProductResult> DeleteVariantAsync(int variantId)
    {
        await using var transaction = await BeginVariantWriteAsync();
        var variants = await _unitOfWork.ProductVariants
            .FindAsync(pv => pv.Id == variantId);
        var variant = variants.FirstOrDefault();

        if (variant == null)
            return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy variant với ID {variantId}");

        if (variant.IsActive)
        {
            var transitionError = await PrepareLastVariantDeactivationAsync(variant);
            if (transitionError != null)
                return ProductResult.Fail(ProductErrorType.ValidationError, transitionError);
        }

        var productId = variant.ProductId;
        var wasActive = variant.IsActive;

        if (await _unitOfWork.OrderItems.AnyAsync(i => i.ProductVariantId == variantId) ||
            await _unitOfWork.PriceSchedules.AnyAsync(schedule => schedule.ProductVariantId == variantId) ||
            await _unitOfWork.CartItems.AnyAsync(item => item.ProductVariantId == variantId))
            variant.IsActive = false;
        else
            _unitOfWork.ProductVariants.Remove(variant);
        await _unitOfWork.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        await TryIndexProductAsync(productId);
        if (_notifier != null && wasActive)
        {
            await _notifier.NotifyStockChangedAsync(productId, 0, variantId);
            await _notifier.NotifyPriceChangedAsync(productId, variantId);
        }

        // Get product for result
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == productId);
        var product = products.FirstOrDefault();

        return product != null 
            ? ProductResult.Ok(product) 
            : ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");
    }

    #endregion

    private Task<bool> CanEnableVariantAsync(int productId)
    {
        var now = DateTimeOffset.UtcNow;
        return _unitOfWork.PriceSchedules.Query().AllAsync(s =>
            s.ProductId != productId || s.ProductVariantId != null || s.IsCancelled ||
            (s.EndsAt.HasValue && s.EndsAt <= now));
    }

    private async Task<string?> PrepareLastVariantDeactivationAsync(ProductVariant variant)
    {
        if (!variant.IsActive)
            return null;

        var activeVariantCount = await _unitOfWork.ProductVariants.Query()
            .CountAsync(item => item.ProductId == variant.ProductId && item.IsActive);
        if (activeVariantCount != 1)
            return null;

        var now = DateTimeOffset.UtcNow;
        var hasActiveOrUpcomingVariantSchedule = await _unitOfWork.PriceSchedules.Query()
            .AnyAsync(schedule =>
                schedule.ProductId == variant.ProductId &&
                schedule.ProductVariantId != null &&
                !schedule.IsCancelled &&
                (!schedule.EndsAt.HasValue || schedule.EndsAt > now));

        if (hasActiveOrUpcomingVariantSchedule)
            return "Hãy hủy hoặc kết thúc các lịch giá biến thể đang chạy hoặc sắp tới trước khi tắt biến thể cuối cùng.";

        var product = await _unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product == null)
            return "Không tìm thấy sản phẩm chứa biến thể.";

        product.Price = variant.Price;
        product.StockQuantity = variant.StockQuantity;
        product.PriceRevision++;
        product.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginVariantWriteAsync()
    {
        if ((_unitOfWork.DatabaseProviderName ?? string.Empty).Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return null;
        return await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    }
}
