using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IProductPricingService? _pricing;

    public ProductService(IUnitOfWork unitOfWork, TimeProvider? timeProvider = null,
        IProductPricingService? pricing = null)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
        var catalogQuery = _unitOfWork.Products.Query().AsNoTracking().Where(p => p.IsActive && !p.IsDeleted);

        // Filter by category
        if (categoryId.HasValue)
            catalogQuery = catalogQuery.Where(p => p.CategoryId == categoryId.Value);

        // Filter by search
        if (!string.IsNullOrEmpty(search))
            catalogQuery = catalogQuery.Where(p => p.Name.Contains(search) || p.Description!.Contains(search));

        if (_pricing == null)
            return await GetShopViewModelInMemoryAsync(catalogQuery, categoryId, search, minPrice, maxPrice, sortBy, page, pageSize);

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
        var totalItems = await priced.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var pagePrices = await priced
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var pageIds = pagePrices.Select(p => p.ProductId).ToList();
        var loaded = await PricedQuery().Where(p => pageIds.Contains(p.Id))
            .Include(p => p.Category).Include(p => p.Images).ToListAsync();
        ApplyPricing(loaded);
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
            TotalPages = totalPages,
            PageSize = pageSize
        };
    }

    private async Task<ShopViewModel> GetShopViewModelInMemoryAsync(IQueryable<Product> catalogQuery,
        int? categoryId, string? search, decimal? minPrice, decimal? maxPrice, string? sortBy, int page, int pageSize)
    {
        var products = await PricedQuery().Where(p => catalogQuery.Select(x => x.Id).Contains(p.Id))
            .Include(p => p.Category).Include(p => p.Images).ToListAsync();
        ApplyPricing(products);
        IEnumerable<Product> filtered = products;
        if (minPrice.HasValue) filtered = filtered.Where(p => p.DisplayMaxPrice >= minPrice.Value);
        if (maxPrice.HasValue) filtered = filtered.Where(p => p.DisplayMinPrice <= maxPrice.Value);
        filtered = sortBy switch
        {
            "price_asc" => filtered.OrderBy(p => p.DisplayMinPrice),
            "price_desc" => filtered.OrderByDescending(p => p.DisplayMaxPrice),
            "name" => filtered.OrderBy(p => p.Name),
            "newest" => filtered.OrderByDescending(p => p.CreatedAt),
            _ => filtered.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt)
        };
        var all = filtered.ToList();
        var categories = await _unitOfWork.Categories.Query().Include(c => c.Products.Where(p => p.IsActive)).ToListAsync();
        return new ShopViewModel
        {
            Products = all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), Categories = categories,
            SelectedCategoryId = categoryId, SearchTerm = search, MinPrice = minPrice, MaxPrice = maxPrice,
            SortBy = sortBy, CurrentPage = page, TotalPages = (int)Math.Ceiling(all.Count / (double)pageSize), PageSize = pageSize
        };
    }

    public async Task<List<Product>> GetRelatedProductsAsync(int productId, int count = 4)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null) return new List<Product>();

        var related = await PricedQuery()
            .Where(p => p.IsActive && p.CategoryId == product.CategoryId && p.Id != productId)
            .Include(p => p.Images)
            .Take(count)
            .ToListAsync();
        ApplyPricing(related);
        return related;
    }

    private IQueryable<Product> PricedQuery() => _unitOfWork.Products.Query().AsNoTracking()
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
