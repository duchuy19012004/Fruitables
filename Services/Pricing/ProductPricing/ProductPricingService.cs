using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Pricing.ProductPricing;

public sealed class ProductPricingService : IProductPricingService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ProductPricingService(ApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<PriceQuote?> GetQuoteAsync(int productId, int? variantId = null, DateTimeOffset? at = null)
    {
        var key = new PriceTargetKey(productId, variantId);
        var quotes = await GetQuotesAsync([key], at);
        return quotes.GetValueOrDefault(key);
    }

    public async Task<IReadOnlyDictionary<PriceTargetKey, PriceQuote>> GetQuotesAsync(
        IEnumerable<PriceTargetKey> targets,
        DateTimeOffset? at = null)
    {
        var keys = targets.Distinct().ToList();
        var productIds = keys.Select(k => k.ProductId).Distinct().ToList();
        var variantIds = keys.Where(k => k.ProductVariantId.HasValue).Select(k => k.ProductVariantId!.Value).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id);
        var variants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id) && v.IsActive)
            .ToDictionaryAsync(v => v.Id);
        var instant = at ?? _timeProvider.GetUtcNow();
        var schedules = await _db.PriceSchedules
            .Where(s => productIds.Contains(s.ProductId) && !s.IsCancelled && s.StartsAt <= instant &&
                (!s.EndsAt.HasValue || instant < s.EndsAt.Value))
            .ToListAsync();
        var result = new Dictionary<PriceTargetKey, PriceQuote>();
        foreach (var target in keys)
        {
            if (!products.TryGetValue(target.ProductId, out var product)) continue;
            var basePrice = product.Price;
            if (target.ProductVariantId.HasValue)
            {
                if (!variants.TryGetValue(target.ProductVariantId.Value, out var variant) || variant.ProductId != target.ProductId) continue;
                basePrice = variant.Price;
            }
            var quote = CalculateQuote(basePrice,
                schedules.Where(s => s.ProductId == target.ProductId && s.ProductVariantId == target.ProductVariantId), instant);
            result[target] = quote with { ProductId = target.ProductId, ProductVariantId = target.ProductVariantId };
        }
        return result;
    }

    public IEnumerable<ProductPriceProjection> ProjectCatalogPrices(IQueryable<Product> products, DateTimeOffset? at = null)
    {
        var instant = at ?? _timeProvider.GetUtcNow();
        // Materialize and compute prices in memory: EF Core cannot translate Min/Max over
        // a subquery that selects the active PriceSchedule (SqlException 130).
        return products
            .AsNoTracking()
            .Include(p => p.Variants.Where(v => v.IsActive)).ThenInclude(v => v.PriceSchedules)
            .Include(p => p.PriceSchedules)
            .AsEnumerable()
            .Select(product => new ProductPriceProjection
            {
                ProductId = product.Id,
                Name = product.Name,
                CreatedAt = product.CreatedAt,
                IsFeatured = product.IsFeatured,
                MinPrice = ComputeCatalogMinPrice(product, instant),
                MaxPrice = ComputeCatalogMaxPrice(product, instant)
            });
    }

    private static decimal ComputeCatalogMinPrice(Product product, DateTimeOffset instant)
    {
        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        if (activeVariants.Count > 0)
        {
            return activeVariants.Min(variant =>
                ComputeEffectivePrice(variant.Price, variant.PriceSchedules, instant));
        }
        return ComputeEffectivePrice(product.Price, product.PriceSchedules, instant);
    }

    private static decimal ComputeCatalogMaxPrice(Product product, DateTimeOffset instant)
    {
        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        if (activeVariants.Count > 0)
        {
            return activeVariants.Max(variant =>
                ComputeEffectivePrice(variant.Price, variant.PriceSchedules, instant));
        }
        return ComputeEffectivePrice(product.Price, product.PriceSchedules, instant);
    }

    private static decimal ComputeEffectivePrice(
        decimal basePrice,
        IEnumerable<PriceSchedule> schedules,
        DateTimeOffset instant) =>
        PriceCalculator.CalculateQuote(basePrice, schedules, instant).EffectivePrice;

    public static PriceQuote CalculateQuote(decimal basePrice, IEnumerable<PriceSchedule> schedules, DateTimeOffset instant) =>
        PriceCalculator.CalculateQuote(basePrice, schedules, instant);
}
