using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Pricing.ProductPricing;

namespace Fruitables.Services.Catalog.Products;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IProductPricingService _pricing;
    private readonly IJsonDocumentSerializer _serializer;

    public ProductService(IUnitOfWork unitOfWork, TimeProvider timeProvider,
        IProductPricingService pricing, IJsonDocumentSerializer? serializer = null)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _pricing = pricing;
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        var products = await PricedQuery()
            .Where(p => p.IsActive)
            .Include(p => p.Category)
            .ToListAsync();
        ProductAggregateJson.Hydrate(products, _serializer);
        await ApplyPricingAsync(products);
        return products;
    }

    public async Task<List<Product>> GetFeaturedProductsAsync(int count = 8)
    {
        var products = await PricedQuery()
            .Where(p => p.IsActive && p.IsFeatured)
            .Include(p => p.Category)
            .Take(count)
            .ToListAsync();
        ProductAggregateJson.Hydrate(products, _serializer);
        await ApplyPricingAsync(products);
        return products;
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(int categoryId)
    {
        var products = await PricedQuery()
            .Where(p => p.IsActive && p.CategoryId == categoryId)
            .Include(p => p.Category)
            .ToListAsync();
        ProductAggregateJson.Hydrate(products, _serializer);
        await ApplyPricingAsync(products);
        return products;
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var product = await PricedQuery()
            .Include(p => p.Category)
            .Include(p => p.Reviews).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product != null)
        {
            ProductAggregateJson.Hydrate([product], _serializer);
            await ApplyPricingAsync([product]);
        }
        return product;
    }

    public async Task<Product?> GetProductBySlugAsync(string slug)
    {
        var product = await PricedQuery()
            .Include(p => p.Category)
            .Include(p => p.Reviews).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Slug == slug);
        if (product != null)
        {
            ProductAggregateJson.Hydrate([product], _serializer);
            await ApplyPricingAsync([product]);
        }
        return product;
    }

    public async Task<ShopViewModel> GetShopViewModelAsync(int? categoryId, string? search, decimal? minPrice, decimal? maxPrice, string? sortBy, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 60);
        var catalogQuery = _unitOfWork.Products.Query().AsNoTracking().Where(p => p.IsActive && !p.IsDeleted);

        // Filter by category
        if (categoryId.HasValue)
            catalogQuery = catalogQuery.Where(p => p.CategoryId == categoryId.Value);

        // Filter by search
        if (!string.IsNullOrEmpty(search))
            catalogQuery = catalogQuery.Where(p => p.Name.Contains(search) || p.Description!.Contains(search));

        var priced = _pricing.ProjectCatalogPrices(catalogQuery);
        if (minPrice.HasValue) priced = priced.Where(p => p.MaxPrice >= minPrice.Value);
        if (maxPrice.HasValue) priced = priced.Where(p => p.MinPrice <= maxPrice.Value);
        priced = sortBy switch
        {
            "price_asc" => priced.OrderBy(p => p.MinPrice),
            "price_desc" => priced.OrderByDescending(p => p.MaxPrice),
            "name" => priced.OrderBy(p => p.Name),
            "newest" => priced.OrderByDescending(p => p.CreatedAt),
            _ => priced.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt)
        };
        var totalItems = priced.Count();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0) page = Math.Min(page, totalPages);
        var pagePrices = priced
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var pageIds = pagePrices.Select(p => p.ProductId).ToList();
        var loaded = await PricedQuery().Where(p => pageIds.Contains(p.Id))
            .Include(p => p.Category).ToListAsync();
        ProductAggregateJson.Hydrate(loaded, _serializer);
        await ApplyPricingAsync(loaded);
        var productsById = loaded.ToDictionary(p => p.Id);
        var products = pageIds.Where(productsById.ContainsKey).Select(id => productsById[id]).ToList();

        var categories = await _unitOfWork.Categories.Query()
            .Include(c => c.Products.Where(p => p.IsActive))
            .ToListAsync();

        return new ShopViewModel
        {
            Products = products,
            Categories = categories,
            SelectedCategoryId = categoryId,
            SearchTerm = search,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            SortBy = sortBy,
            CurrentPage = page,
            TotalItems = totalItems,
            TotalPages = totalPages,
            PageSize = pageSize
        };
    }

    public async Task<List<Product>> GetRelatedProductsAsync(int productId, int count = 4)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null) return new List<Product>();

        var related = await PricedQuery()
            .Where(p => p.IsActive && p.CategoryId == product.CategoryId && p.Id != productId)
            .Take(count)
            .ToListAsync();
        ProductAggregateJson.Hydrate(related, _serializer);
        await ApplyPricingAsync(related);
        return related;
    }

    private IQueryable<Product> PricedQuery() => _unitOfWork.Products.Query().AsNoTracking()
        .Include(p => p.Variants.Where(v => v.IsActive));

    private async Task ApplyPricingAsync(IReadOnlyCollection<Product> products)
    {
        var now = _timeProvider.GetUtcNow();
        var targets = products.SelectMany(product =>
        {
            var variants = product.Variants.Where(v => v.IsActive).ToList();
            return variants.Count > 0
                ? variants.Select(variant => new PriceTargetKey(product.Id, variant.Id))
                : [new PriceTargetKey(product.Id, null)];
        }).ToList();
        var quotes = await _pricing.GetQuotesAsync(targets, now);

        foreach (var product in products)
        {
            var variants = product.Variants.Where(v => v.IsActive).ToList();
            if (variants.Count > 0)
            {
                var prices = new List<decimal>();
                foreach (var variant in variants)
                {
                    var quote = quotes.GetValueOrDefault(new PriceTargetKey(product.Id, variant.Id))
                        ?? new PriceQuote(product.Id, variant.Id, variant.Price, variant.Price, null);
                    variant.SalePrice = quote.IsDiscounted ? quote.EffectivePrice : null;
                    variant.DisplayPrice = quote.EffectivePrice;
                    prices.Add(quote.EffectivePrice);
                }
                product.DisplayMinPrice = prices.Min();
                product.DisplayMaxPrice = prices.Max();
                product.SalePrice = null;
                product.StockQuantity = variants.Sum(v => v.StockQuantity);
            }
            else
            {
                var quote = quotes.GetValueOrDefault(new PriceTargetKey(product.Id, null))
                    ?? new PriceQuote(product.Id, null, product.Price, product.Price, null);
                product.SalePrice = quote.IsDiscounted ? quote.EffectivePrice : null;
                product.DisplayMinPrice = quote.EffectivePrice;
                product.DisplayMaxPrice = quote.EffectivePrice;
            }
        }
    }
}

internal static class ProductAggregateJson
{
    public static void Hydrate(IEnumerable<Product> products, IJsonDocumentSerializer serializer)
    {
        foreach (var product in products)
        {
            var images = ReadImages(product.ImagesJson, serializer);
            var tags = ReadTags(product.TagsJson, serializer);

            product.Images = images.Images
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .Select((image, index) => new ProductImage
                {
                    Id = index + 1,
                    ProductId = product.Id,
                    ImageUrl = image.Url,
                    IsPrimary = image.IsPrimary,
                    SortOrder = image.SortOrder,
                    Product = product
                })
                .ToList();
            product.Tags = tags.Tags
                .Select((tag, index) => new ProductTag
                {
                    Id = index + 1,
                    Name = tag.Name,
                    Slug = tag.Slug,
                    Products = [product]
                })
                .ToList();
        }
    }

    public static ProductImagesDocument ReadImages(string json, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new ProductImagesDocument();
        return serializer.Deserialize<ProductImagesDocument>(json);
    }

    public static ProductTagsDocument ReadTags(string json, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new ProductTagsDocument();
        return serializer.Deserialize<ProductTagsDocument>(json);
    }

    public static List<ProductImage> ReadImageModels(Product product, IJsonDocumentSerializer serializer) =>
        ReadImages(product.ImagesJson, serializer).Images
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .Select((image, index) => new ProductImage
            {
                Id = index + 1,
                ProductId = product.Id,
                ImageUrl = image.Url,
                IsPrimary = image.IsPrimary,
                SortOrder = image.SortOrder,
                Product = product
            })
            .ToList();

    public static string SerializeImages(IEnumerable<ProductImage> images, IJsonDocumentSerializer serializer) =>
        serializer.Serialize(new ProductImagesDocument
        {
            Images = images
                .OrderBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Select(image => new ProductImageDocument
                {
                    Url = image.ImageUrl,
                    StorageKey = image.ImageUrl.TrimStart('/'),
                    IsPrimary = image.IsPrimary,
                    SortOrder = image.SortOrder
                })
                .ToList()
        });

    public static string SerializeTags(IEnumerable<ProductTag> tags, IJsonDocumentSerializer serializer) =>
        serializer.Serialize(new ProductTagsDocument
        {
            Tags = tags
                .Select(tag => new ProductTagDocument { Name = tag.Name, Slug = tag.Slug })
                .ToList()
        });
}
