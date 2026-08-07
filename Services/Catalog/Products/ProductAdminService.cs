using System.Text.Json;
using System.Text.RegularExpressions;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Auditing;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Orders;
using Fruitables.Services.Reviews;

namespace Fruitables.Services.Catalog.Products;

public class ProductAdminService : IProductAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageUploadService _imageUploadService;
    private readonly IIndexingService _indexing;
    private readonly ILogger<ProductAdminService> _logger;
    private readonly IRealtimeNotifier? _notifier;
    private readonly ApplicationDbContext? _dbContext;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly IAuditLogWriter? _auditLogWriter;

    public ProductAdminService(
        IUnitOfWork unitOfWork,
        IImageUploadService imageUploadService,
        IIndexingService indexing,
        ILogger<ProductAdminService> logger,
        IRealtimeNotifier? notifier = null,
        ApplicationDbContext? dbContext = null,
        IJsonDocumentSerializer? serializer = null,
        IAuditLogWriter? auditLogWriter = null)
    {
        _unitOfWork = unitOfWork;
        _imageUploadService = imageUploadService;
        _indexing = indexing;
        _logger = logger;
        _notifier = notifier;
        _dbContext = dbContext;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _auditLogWriter = auditLogWriter ?? (dbContext == null ? null : new AuditLogWriter(dbContext));
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

    private Task WriteAuditAsync(string action, int productId, string details) =>
        _auditLogWriter?.WriteAsync(
            action,
            "Product",
            productId,
            0,
            newValue: JsonSerializer.Serialize(new { details })) ?? Task.CompletedTask;

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
        var query = _unitOfWork.Products.Query().AsNoTracking();

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
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        ProductAggregateJson.Hydrate(products, _serializer);

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
        var product = await _unitOfWork.Products.Query().AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product != null)
            ProductAggregateJson.Hydrate([product], _serializer);
        return product;
    }

    public async Task<ProductResult> CreateProductAsync(CreateProductRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Tên sản phẩm không được để trống");

        if (request.Price <= 0)
            return ProductResult.Fail(ProductErrorType.ValidationError, "Giá phải lớn hơn 0");
        if (!IsValidStock(request.Unit, request.StockQuantity) ||
            !QuantityRules.IsValid(request.Unit, request.MinOrderQuantity, MinimumStep(request.Unit)))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Số lượng sản phẩm không hợp lệ");

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
            IsDeleted = false,
            ImagesJson = _serializer.Serialize(new Fruitables.Models.Json.ProductImagesDocument()),
            TagsJson = _serializer.Serialize(new Fruitables.Models.Json.ProductTagsDocument())
        };

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();
        await WriteAuditAsync("Create", product.Id, $"Tạo sản phẩm: {product.Name}");

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

        if (!IsValidStock(request.Unit, request.StockQuantity) ||
            !QuantityRules.IsValid(request.Unit, request.MinOrderQuantity, MinimumStep(request.Unit)))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Số lượng sản phẩm không hợp lệ");

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
        await WriteAuditAsync("Update", product.Id, $"Cập nhật sản phẩm: {product.Name}");

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
        await WriteAuditAsync("SoftDelete", product.Id, "Chuyển sản phẩm vào thùng rác");

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
        await WriteAuditAsync("Restore", product.Id, "Khôi phục sản phẩm từ thùng rác");

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
        if ((await GetPriceSchedulesAsync()).Any(schedule => schedule.ProductId == id))
            return ProductResult.Fail(ProductErrorType.ValidationError,
                "Không thể xóa vĩnh viễn sản phẩm đã có lịch sử giá. Hãy sử dụng chức năng xóa mềm.");
        var productInCart = _dbContext?.Database.IsSqlServer() == true
            ? await IsProductInCartAsync(id, null)
            : await _unitOfWork.CartItems.AnyAsync(item => item.ProductId == id);
        if (productInCart)
            return ProductResult.Fail(ProductErrorType.ValidationError,
                "Không thể xóa vĩnh viễn sản phẩm đang nằm trong giỏ hàng. Hãy sử dụng chức năng xóa mềm.");
        if (await IsReferencedByComboPayloadAsync(id))
            return ProductResult.Fail(ProductErrorType.ValidationError,
                "Không thể xóa vĩnh viễn sản phẩm đang được tham chiếu bởi combo.");

        // Delete related data
        var imageUrls = ProductAggregateJson.ReadImages(product.ImagesJson, _serializer)
            .Images.Select(image => image.Url).ToList();

        var variants = await _unitOfWork.ProductVariants
            .FindAsync(pv => pv.ProductId == id);
        foreach (var variant in variants)
        {
            _unitOfWork.ProductVariants.Remove(variant);
        }

        var productId = product.Id;
        _unitOfWork.Products.Remove(product);
        await _unitOfWork.SaveChangesAsync();
        await WriteAuditAsync("HardDelete", productId, "Xóa vĩnh viễn sản phẩm");

        foreach (var imageUrl in imageUrls)
        {
            try
            {
                await _imageUploadService.DeleteImageAsync(imageUrl);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not delete product image file {ImageUrl}", imageUrl);
            }
        }

        // Missing product deactivates knowledge chunks in IndexingService.
        await TryIndexProductAsync(productId);

        return ProductResult.Ok(product);
    }

    #endregion

    #region Image Management

    public async Task<ProductResult> AddImagesAsync(int productId, List<IFormFile> files)
    {
        if (files.Count > 10)
            return ProductResult.Fail(ProductErrorType.ValidationError, "Mỗi lần chỉ được tải lên tối đa 10 ảnh");

        // Validate files
        foreach (var file in files)
        {
            if (!_imageUploadService.IsValidImageFile(file))
                return ProductResult.Fail(ProductErrorType.InvalidFileType, "File không phải định dạng ảnh hợp lệ");

            if (!_imageUploadService.IsValidFileSize(file))
                return ProductResult.Fail(ProductErrorType.FileTooLarge, "File vượt quá kích thước cho phép (5MB)");
        }

        var uploadedUrls = new List<string>();
        await using var transaction = await BeginProductWriteAsync();
        try
        {
            var products = await _unitOfWork.Products
                .FindAsync(p => p.Id == productId);
            var product = products.FirstOrDefault();
            if (product == null)
                return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

            var existingImages = ProductAggregateJson.ReadImageModels(product, _serializer)
                .OrderBy(image => image.SortOrder)
                .ToList();
            var maxSortOrder = existingImages.Any() ? existingImages.Max(i => i.SortOrder) : -1;

            foreach (var file in files)
            {
                var imageUrl = await _imageUploadService.UploadProductImageAsync(file);
                uploadedUrls.Add(imageUrl);

                existingImages.Add(new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = imageUrl,
                    IsPrimary = !existingImages.Any() && maxSortOrder == -1,
                    SortOrder = ++maxSortOrder
                });
            }

            product.ImagesJson = ProductAggregateJson.SerializeImages(existingImages, _serializer);
            product.AssetRevision++;
            await _unitOfWork.SaveChangesAsync();
            await WriteAuditAsync("ImageUpload", productId, $"Upload {uploadedUrls.Count} ảnh");
            if (transaction != null)
                await transaction.CommitAsync();

            return ProductResult.Ok(product);
        }
        catch (Exception exception)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            foreach (var uploadedUrl in uploadedUrls)
            {
                try
                {
                    await _imageUploadService.DeleteImageAsync(uploadedUrl);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(cleanupException, "Could not remove rolled-back product image {ImageUrl}", uploadedUrl);
                }
            }

            _logger.LogError(exception, "Could not add images for product {ProductId}", productId);
            return ProductResult.Fail(ProductErrorType.ValidationError, exception.Message);
        }
    }

    public async Task<ProductResult> SetPrimaryImageAsync(int productId, int imageId)
    {
        await using var transaction = await BeginProductWriteAsync();
        try
        {
            var product = (await _unitOfWork.Products.FindAsync(p => p.Id == productId)).FirstOrDefault();
            if (product == null)
                return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

            var images = ProductAggregateJson.ReadImageModels(product, _serializer);
            var targetImage = images.FirstOrDefault(i => i.Id == imageId);
            if (targetImage == null)
                return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy ảnh với ID {imageId}");

            foreach (var image in images)
                image.IsPrimary = false;
            targetImage.IsPrimary = true;

            product.ImagesJson = ProductAggregateJson.SerializeImages(images, _serializer);
            product.AssetRevision++;
            await _unitOfWork.SaveChangesAsync();
            await WriteAuditAsync("ImagePrimary", productId, $"Đặt ảnh {imageId} làm ảnh chính");
            if (transaction != null)
                await transaction.CommitAsync();

            return ProductResult.Ok(product);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return ProductResult.Fail(ProductErrorType.ValidationError, "Sản phẩm vừa được cập nhật. Vui lòng tải lại trang.");
        }
    }

    public async Task<ProductResult> DeleteImageAsync(int productId, int imageId)
    {
        await using var transaction = await BeginProductWriteAsync();
        try
        {
            var product = (await _unitOfWork.Products.FindAsync(p => p.Id == productId)).FirstOrDefault();
            if (product == null)
                return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

            var images = ProductAggregateJson.ReadImageModels(product, _serializer);
            var image = images.FirstOrDefault(candidate => candidate.Id == imageId);
            if (image == null)
                return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy ảnh với ID {imageId}");

            var remainingImages = images.Where(candidate => candidate.Id != imageId).ToList();
            if (image.IsPrimary)
            {
                var replacement = remainingImages.OrderBy(pi => pi.SortOrder).FirstOrDefault();
                if (replacement != null)
                    replacement.IsPrimary = true;
            }

            product.ImagesJson = ProductAggregateJson.SerializeImages(remainingImages, _serializer);
            product.AssetRevision++;
            await _unitOfWork.SaveChangesAsync();
            await WriteAuditAsync("ImageDelete", productId, $"Xóa ảnh {imageId}");
            if (transaction != null)
                await transaction.CommitAsync();

            try
            {
                await _imageUploadService.DeleteImageAsync(image.ImageUrl);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not delete product image file {ImageUrl}", image.ImageUrl);
            }

            return ProductResult.Ok(product);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return ProductResult.Fail(ProductErrorType.ValidationError, "Sản phẩm vừa được cập nhật. Vui lòng tải lại trang.");
        }
    }

    public async Task<ProductResult> ReorderImagesAsync(int productId, List<int> imageIds)
    {
        await using var transaction = await BeginProductWriteAsync();
        try
        {
            var product = (await _unitOfWork.Products.FindAsync(p => p.Id == productId)).FirstOrDefault();
            if (product == null)
                return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");

            var images = ProductAggregateJson.ReadImageModels(product, _serializer)
                .OrderBy(image => image.SortOrder)
                .ToList();
            var ordered = imageIds
                .Select(id => images.FirstOrDefault(image => image.Id == id))
                .Where(image => image != null)
                .Cast<ProductImage>()
                .Concat(images.Where(image => !imageIds.Contains(image.Id)))
                .ToList();
            for (var index = 0; index < ordered.Count; index++)
                ordered[index].SortOrder = index;

            product.ImagesJson = ProductAggregateJson.SerializeImages(ordered, _serializer);
            product.AssetRevision++;
            await _unitOfWork.SaveChangesAsync();
            await WriteAuditAsync("ImageReorder", productId, "Sắp xếp lại ảnh sản phẩm");
            if (transaction != null)
                await transaction.CommitAsync();

            return ProductResult.Ok(product);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return ProductResult.Fail(ProductErrorType.ValidationError, "Sản phẩm vừa được cập nhật. Vui lòng tải lại trang.");
        }
    }

    #endregion

    #region Tag Management

    public async Task<ProductResult> UpdateTagsAsync(int productId, List<string> tagNames)
    {
        await using var transaction = await BeginProductWriteAsync();
        try
        {
            var product = await _unitOfWork.Products.Query()
                .FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
                return ProductResult.Fail(ProductErrorType.NotFound, $"Không tìm thấy sản phẩm với ID {productId}");
            if (tagNames.Any(string.IsNullOrWhiteSpace))
                return ProductResult.Fail(ProductErrorType.ValidationError, "Tên tag không được để trống");

            tagNames = tagNames
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            product.TagsJson = ProductAggregateJson.SerializeTags(
                tagNames.Select((tagName, index) => new ProductTag
                {
                    Id = index + 1,
                    Name = tagName,
                    Slug = GenerateSlug(tagName)
                }),
                _serializer);
            product.AssetRevision++;
            await _unitOfWork.SaveChangesAsync();
            await WriteAuditAsync("TagUpdate", productId, "Cập nhật tags sản phẩm");
            if (transaction != null)
                await transaction.CommitAsync();

            return ProductResult.Ok(product);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            return ProductResult.Fail(ProductErrorType.ValidationError, "Sản phẩm vừa được cập nhật. Vui lòng tải lại trang.");
        }
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

        if (!IsValidStock(product.Unit, request.StockQuantity))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Số lượng tồn kho không hợp lệ");

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
        await WriteAuditAsync("VariantCreate", product.Id, $"Tạo biến thể {variant.SKU}");
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
        var product = await _unitOfWork.Products.GetByIdAsync(variant.ProductId);
        if (product == null || !IsValidStock(product.Unit, request.StockQuantity))
            return ProductResult.Fail(ProductErrorType.ValidationError, "Số lượng tồn kho không hợp lệ");

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
        await WriteAuditAsync("VariantUpdate", variant.ProductId, $"Cập nhật biến thể {variant.SKU}");

        // Get product for result
        var products = await _unitOfWork.Products
            .FindAsync(p => p.Id == variant.ProductId);
        var refreshedProduct = products.FirstOrDefault();
        if (transaction != null) await transaction.CommitAsync();
        if (_notifier != null && refreshedProduct != null && (oldStock != variant.StockQuantity || wasActive != variant.IsActive))
        {
            await _notifier.NotifyStockChangedAsync(refreshedProduct.Id, variant.StockQuantity, variant.Id);
            if (wasActive != variant.IsActive) await _notifier.NotifyPriceChangedAsync(refreshedProduct.Id, variant.Id);
        }
        if (refreshedProduct != null) await TryIndexProductAsync(refreshedProduct.Id);

        return refreshedProduct != null
            ? ProductResult.Ok(refreshedProduct)
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
            (await GetPriceSchedulesAsync()).Any(schedule => schedule.ProductVariantId == variantId) ||
            (_dbContext?.Database.IsSqlServer() == true
                ? await IsProductInCartAsync(variant.ProductId, variantId)
                : await _unitOfWork.CartItems.AnyAsync(item => item.ProductVariantId == variantId)))
            variant.IsActive = false;
        else
            _unitOfWork.ProductVariants.Remove(variant);
        await _unitOfWork.SaveChangesAsync();
        await WriteAuditAsync("VariantDelete", productId, $"Xóa biến thể {variantId}");
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

    private static decimal MinimumStep(string? unit) =>
        string.Equals(unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase) ? 0.1m : 1m;

    private static bool IsValidStock(string? unit, decimal quantity) =>
        quantity >= 0 && (quantity == 0 || QuantityRules.IsValid(unit, quantity, MinimumStep(unit)));

    private async Task<bool> CanEnableVariantAsync(int productId)
    {
        var now = DateTimeOffset.UtcNow;
        var schedules = await GetPriceSchedulesAsync();
        return schedules.All(s =>
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
        var hasActiveOrUpcomingVariantSchedule = (await GetPriceSchedulesAsync()).Any(schedule =>
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

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginProductWriteAsync()
    {
        if ((_unitOfWork.DatabaseProviderName ?? string.Empty).Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return null;
        return await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    }

    private async Task<List<PriceSchedule>> GetPriceSchedulesAsync()
    {
        if (_dbContext == null)
            return [];

        var promotions = await _dbContext.Promotions.AsNoTracking()
            .Where(promotion => promotion.Type == "price-schedule")
            .ToListAsync();
        return promotions.Select(promotion =>
        {
            var payload = _serializer.Deserialize<Fruitables.Models.Json.PriceSchedulePayload>(promotion.PayloadJson);
            return new PriceSchedule
            {
                Id = payload.LegacyScheduleId ?? ParseLegacyScheduleId(promotion.Code) ?? promotion.Id,
                ProductId = payload.ProductId,
                ProductVariantId = payload.ProductVariantId,
                DiscountType = payload.DiscountType,
                Value = payload.Value,
                StartsAt = payload.StartsAt,
                EndsAt = payload.EndsAt,
                IsCancelled = payload.IsCancelled,
                CancelledAt = payload.CancelledAt,
                CancelledByAdminId = payload.CancelledByAdminId,
                CancellationReason = payload.CancellationReason,
                Revision = payload.Revision,
                CreatedByAdminId = payload.CreatedByAdminId,
                CreatedAt = payload.CreatedAt,
                UpdatedAt = payload.UpdatedAt
            };
        }).ToList();
    }

    private static int? ParseLegacyScheduleId(string? code)
    {
        const string prefix = "price-schedule:";
        return code != null && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(code[prefix.Length..], out var id) && id > 0
            ? id
            : null;
    }

    private async Task<bool> IsProductInCartAsync(int productId, int? variantId)
    {
        if (_dbContext is null)
            return false;
        var carts = await _dbContext.Carts.AsNoTracking().Select(cart => cart.LinesJson).ToListAsync();
        return carts.Any(json => _serializer.TryDeserialize<CartLinesDocument>(json, out var document, out _) &&
            document!.Lines.Any(line => line.ProductId == productId &&
                (!variantId.HasValue || line.ProductVariantId == variantId.Value)));
    }

    private async Task<bool> IsReferencedByComboPayloadAsync(int productId)
    {
        if (_dbContext == null)
            return false;

        var payloads = await _dbContext.Promotions.AsNoTracking()
            .Where(promotion => promotion.Type == "combo")
            .Select(promotion => promotion.PayloadJson)
            .ToListAsync();
        return payloads.Any(json =>
            _serializer.TryDeserialize<ComboPayload>(json, out var payload, out _) &&
            payload!.Items.Any(item => item.ProductId == productId));
    }

    public async Task<Dictionary<int, ProductSentimentSummary>> GetSentimentSummariesAsync(IReadOnlyList<int> productIds)
    {
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, ProductSentimentSummary>();

        var rows = _dbContext?.Database.IsSqlServer() == true
            ? (await _dbContext.Reviews.AsNoTracking()
                .Where(review => ids.Contains(review.ProductId) && !review.IsDeleted)
                .ToListAsync())
                .Select(review => new { Review = review, Sentiment = ReviewAggregateJson.Read(review, _serializer).Sentiment })
                .Where(item => item.Sentiment is not null && !item.Sentiment.NeedsManualReview)
                .GroupBy(item => item.Review.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Negative = group.Count(item => item.Sentiment!.Sentiment == SentimentLabel.Negative),
                    Conflict = group.Count(item => item.Sentiment!.HasRatingCommentConflict),
                    Total = group.Count()
                }).ToList()
            : await (
                from r in _unitOfWork.Reviews.Query()
                join s in _unitOfWork.ReviewSentiments.Query() on r.Id equals s.ReviewId
                where ids.Contains(r.ProductId) && !r.IsDeleted && !s.NeedsManualReview
                group s by r.ProductId into g
                select new
                {
                    ProductId = g.Key,
                    Negative = g.Count(x => x.Sentiment == SentimentLabel.Negative),
                    Conflict = g.Count(x => x.HasRatingCommentConflict),
                    Total = g.Count()
                }).ToListAsync();

        return rows.ToDictionary(
            x => x.ProductId,
            x => new ProductSentimentSummary
            {
                NegativeCount = x.Negative,
                ConflictCount = x.Conflict,
                TotalCount = x.Total,
                NegativeRate = x.Total == 0 ? 0 : (float)Math.Round(x.Negative * 100f / x.Total, 1)
            });
    }
}

public sealed class ProductSentimentSummary
{
    public int NegativeCount { get; set; }
    public int ConflictCount { get; set; }
    public int TotalCount { get; set; }
    public float NegativeRate { get; set; }
}
