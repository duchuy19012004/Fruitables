using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services;

public sealed class ProductPricingService : IProductPricingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ProductPricingService(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
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
        var products = await _unitOfWork.Products.Query()
            .Where(p => productIds.Contains(p.Id) && p.IsActive && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id);
        var variants = await _unitOfWork.ProductVariants.Query()
            .Where(v => variantIds.Contains(v.Id) && v.IsActive)
            .ToDictionaryAsync(v => v.Id);
        var instant = at ?? _timeProvider.GetUtcNow();
        var schedules = await _unitOfWork.PriceSchedules.Query()
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

    public IQueryable<ProductPriceProjection> ProjectCatalogPrices(IQueryable<Product> products, DateTimeOffset? at = null)
    {
        var instant = at ?? _timeProvider.GetUtcNow();
        return products.Select(product => new ProductPriceProjection
        {
            ProductId = product.Id,
            Name = product.Name,
            CreatedAt = product.CreatedAt,
            IsFeatured = product.IsFeatured,
            MinPrice = product.Variants.Any(v => v.IsActive)
                ? product.Variants.Where(v => v.IsActive).Min(variant =>
                    variant.PriceSchedules
                        .Where(schedule => !schedule.IsCancelled && schedule.StartsAt <= instant &&
                            (!schedule.EndsAt.HasValue || instant < schedule.EndsAt.Value))
                        .Select(schedule => schedule.DiscountType == DiscountType.FixedPrice
                            ? (decimal?)schedule.Value
                            : Math.Round(variant.Price * (100m - schedule.Value) / 100m, 0))
                        .FirstOrDefault() ?? variant.Price)
                : product.PriceSchedules
                    .Where(schedule => !schedule.IsCancelled && schedule.StartsAt <= instant &&
                        (!schedule.EndsAt.HasValue || instant < schedule.EndsAt.Value))
                    .Select(schedule => schedule.DiscountType == DiscountType.FixedPrice
                        ? (decimal?)schedule.Value
                        : Math.Round(product.Price * (100m - schedule.Value) / 100m, 0))
                    .FirstOrDefault() ?? product.Price,
            MaxPrice = product.Variants.Any(v => v.IsActive)
                ? product.Variants.Where(v => v.IsActive).Max(variant =>
                    variant.PriceSchedules
                        .Where(schedule => !schedule.IsCancelled && schedule.StartsAt <= instant &&
                            (!schedule.EndsAt.HasValue || instant < schedule.EndsAt.Value))
                        .Select(schedule => schedule.DiscountType == DiscountType.FixedPrice
                            ? (decimal?)schedule.Value
                            : Math.Round(variant.Price * (100m - schedule.Value) / 100m, 0))
                        .FirstOrDefault() ?? variant.Price)
                : product.PriceSchedules
                    .Where(schedule => !schedule.IsCancelled && schedule.StartsAt <= instant &&
                        (!schedule.EndsAt.HasValue || instant < schedule.EndsAt.Value))
                    .Select(schedule => schedule.DiscountType == DiscountType.FixedPrice
                        ? (decimal?)schedule.Value
                        : Math.Round(product.Price * (100m - schedule.Value) / 100m, 0))
                    .FirstOrDefault() ?? product.Price
        });
    }

    public static PriceQuote CalculateQuote(decimal basePrice, IEnumerable<PriceSchedule> schedules, DateTimeOffset instant)
    {
        var active = schedules.Where(s => s.IsActiveAt(instant)).OrderByDescending(s => s.StartsAt).FirstOrDefault();
        if (active == null) return new PriceQuote(0, null, basePrice, basePrice, null);

        var effectivePrice = active.DiscountType == DiscountType.FixedPrice
            ? active.Value
            : Math.Round(basePrice * (100m - active.Value) / 100m, 0, MidpointRounding.AwayFromZero);
        return new PriceQuote(0, null, basePrice, effectivePrice, active.Id);
    }
}
