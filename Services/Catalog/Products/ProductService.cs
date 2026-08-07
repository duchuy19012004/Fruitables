using Fruitables.Data;
using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Pricing.ProductPricing;

namespace Fruitables.Services.Catalog.Products;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IProductPricingService _pricing;

    public ProductService(ApplicationDbContext db, TimeProvider timeProvider,
        IProductPricingService pricing)
    {
        _db = db;
        _timeProvider = timeProvider;
        _pricing = pricing;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        var products = await PricedQuery()
            .Where(p => p.IsActive)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .ToListAsync();
        ApplyPricing(products);
        return products;
    }

    public async Task<List<Product>> GetFeaturedProductsAsync(int count = 8)
    {
        var products = await PricedQuery()
            .Where(p => p.IsActive && p.IsFeatured)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Take(count)
            .ToListAsync();
        ApplyPricing(products);
        return products;
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(int categoryId)
    {
        var products = await PricedQuery()
            .Where(p => p.IsActive && p.CategoryId == categoryId)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .ToListAsync();
        ApplyPricing(products);
        return products;
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var product = await PricedQuery()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Reviews).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product != null) ApplyPricing([product]);
        return product;
    }

    public async Task<Product?> GetProductBySlugAsync(string slug)
    {
        var product = await PricedQuery()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Reviews).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Slug == slug);
        if (product != null) ApplyPricing([product]);
        return product;
    }

    public async Task<ShopViewModel> GetShopViewModelAsync(int? categoryId, string? search, decimal? minPrice, decimal? maxPrice, string? sortBy, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 60);
        var catalogQuery = _db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsDeleted);

        // Filter by category
        if (categoryId.HasValue)
            catalogQuery = catalogQuery.Where(p => p.CategoryId == categoryId.Value);

        // Filter by search
        if (!string.IsNullOrEmpty(search))
            catalogQuery = catalogQuery.Where(p => p.Name.Contains(search) || p.Description!.Contains(search));

        var canPageBeforePricing = !minPrice.HasValue && !maxPrice.HasValue &&
            sortBy is not "price_asc" and not "price_desc";
        int totalItems;
        int totalPages;
        List<ProductPriceProjection> pagePrices;

        if (canPageBeforePricing)
        {
            totalItems = await catalogQuery.CountAsync();
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0) page = Math.Min(page, totalPages);

            var orderedCatalog = sortBy switch
            {
                "name" => catalogQuery.OrderBy(p => p.Name),
                "newest" => catalogQuery.OrderByDescending(p => p.CreatedAt),
                _ => catalogQuery.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt)
            };
            pagePrices = _pricing.ProjectCatalogPrices(
                    orderedCatalog.Skip((page - 1) * pageSize).Take(pageSize))
                .ToList();
        }
        else
        {
            IEnumerable<ProductPriceProjection> priced = _pricing.ProjectCatalogPrices(catalogQuery).ToList();
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
            totalItems = priced.Count();
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0) page = Math.Min(page, totalPages);
            pagePrices = priced
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        var pageIds = pagePrices.Select(p => p.ProductId).ToList();
        var loaded = await PricedQuery().Where(p => pageIds.Contains(p.Id))
            .Include(p => p.Category).Include(p => p.Images).ToListAsync();
        ApplyPricing(loaded);
        var productsById = loaded.ToDictionary(p => p.Id);
        var products = pageIds.Where(productsById.ContainsKey).Select(id => productsById[id]).ToList();

        var categories = await _db.Categories
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
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return new List<Product>();

        var related = await PricedQuery()
            .Where(p => p.IsActive && p.CategoryId == product.CategoryId && p.Id != productId)
            .Include(p => p.Images)
            .Take(count)
            .ToListAsync();
        ApplyPricing(related);
        return related;
    }

    private IQueryable<Product> PricedQuery() => _db.Products.AsNoTracking()
        .Include(p => p.Variants.Where(v => v.IsActive)).ThenInclude(v => v.PriceSchedules)
        .Include(p => p.PriceSchedules);

    private void ApplyPricing(IEnumerable<Product> products)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var product in products)
        {
            var variants = product.Variants.Where(v => v.IsActive).ToList();
            if (variants.Count > 0)
            {
                var prices = new List<decimal>();
                foreach (var variant in variants)
                {
                    var quote = ProductPricingService.CalculateQuote(variant.Price, variant.PriceSchedules, now);
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
                var quote = ProductPricingService.CalculateQuote(product.Price, product.PriceSchedules, now);
                product.SalePrice = quote.IsDiscounted ? quote.EffectivePrice : null;
                product.DisplayMinPrice = quote.EffectivePrice;
                product.DisplayMaxPrice = quote.EffectivePrice;
            }
        }
    }
}
