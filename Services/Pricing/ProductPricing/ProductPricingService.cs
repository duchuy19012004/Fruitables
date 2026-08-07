using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Data;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Pricing.ProductPricing;

public sealed class ProductPricingService : IProductPricingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationDbContext? _dbContext;
    private readonly IJsonDocumentSerializer _serializer;

    public ProductPricingService(
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ApplicationDbContext? dbContext = null,
        IJsonDocumentSerializer? serializer = null)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _dbContext = dbContext;
        _serializer = serializer ?? new VersionedJsonSerializer();
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
        var schedules = await GetSchedulesAsync(productIds, instant);
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
        var productList = products
            .AsNoTracking()
            .Include(p => p.Variants.Where(v => v.IsActive))
            .AsEnumerable()
            .Select(product => product)
            .ToList();
        var schedules = GetSchedules(productList.Select(product => product.Id), instant);

        // Materialize and compute prices in memory so JSON payloads stay outside SQL.
        return productList.Select(product => new ProductPriceProjection
            {
                ProductId = product.Id,
                Name = product.Name,
                CreatedAt = product.CreatedAt,
                IsFeatured = product.IsFeatured,
                MinPrice = ComputeCatalogMinPrice(product, schedules, instant),
                MaxPrice = ComputeCatalogMaxPrice(product, schedules, instant)
            });
    }

    private static decimal ComputeCatalogMinPrice(
        Product product,
        IReadOnlyDictionary<PriceTargetKey, List<PriceSchedule>> schedules,
        DateTimeOffset instant)
    {
        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        if (activeVariants.Count > 0)
        {
            return activeVariants.Min(variant =>
                ComputeEffectivePrice(
                    variant.Price,
                    schedules.GetValueOrDefault(new PriceTargetKey(product.Id, variant.Id)) ?? [],
                    instant));
        }
        return ComputeEffectivePrice(
            product.Price,
            schedules.GetValueOrDefault(new PriceTargetKey(product.Id, null)) ?? [],
            instant);
    }

    private static decimal ComputeCatalogMaxPrice(
        Product product,
        IReadOnlyDictionary<PriceTargetKey, List<PriceSchedule>> schedules,
        DateTimeOffset instant)
    {
        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        if (activeVariants.Count > 0)
        {
            return activeVariants.Max(variant =>
                ComputeEffectivePrice(
                    variant.Price,
                    schedules.GetValueOrDefault(new PriceTargetKey(product.Id, variant.Id)) ?? [],
                    instant));
        }
        return ComputeEffectivePrice(
            product.Price,
            schedules.GetValueOrDefault(new PriceTargetKey(product.Id, null)) ?? [],
            instant);
    }

    private static decimal ComputeEffectivePrice(
        decimal basePrice,
        IEnumerable<PriceSchedule> schedules,
        DateTimeOffset instant) =>
        PriceCalculator.CalculateQuote(basePrice, schedules, instant).EffectivePrice;

    public static PriceQuote CalculateQuote(decimal basePrice, IEnumerable<PriceSchedule> schedules, DateTimeOffset instant) =>
        PriceCalculator.CalculateQuote(basePrice, schedules, instant);

    private async Task<List<PriceSchedule>> GetSchedulesAsync(IEnumerable<int> productIds, DateTimeOffset instant)
    {
        if (_dbContext == null)
            return [];

        var ids = productIds.Distinct().ToArray();
        var promotions = await _dbContext.Promotions.AsNoTracking()
            .Where(promotion => promotion.Type == "price-schedule" && promotion.IsActive)
            .ToListAsync();
        return promotions
            .Select(ToSchedule)
            .Where(schedule => ids.Contains(schedule.ProductId) && schedule.IsActiveAt(instant))
            .ToList();
    }

    private IReadOnlyDictionary<PriceTargetKey, List<PriceSchedule>> GetSchedules(
        IEnumerable<int> productIds,
        DateTimeOffset instant)
    {
        if (_dbContext == null)
            return new Dictionary<PriceTargetKey, List<PriceSchedule>>();

        var ids = productIds.Distinct().ToArray();
        var promotions = _dbContext.Promotions.AsNoTracking()
            .Where(promotion => promotion.Type == "price-schedule" && promotion.IsActive)
            .ToList();
        return promotions
            .Select(ToSchedule)
            .Where(schedule => ids.Contains(schedule.ProductId) && schedule.IsActiveAt(instant))
            .GroupBy(schedule => new PriceTargetKey(schedule.ProductId, schedule.ProductVariantId))
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private PriceSchedule ToSchedule(Promotion promotion)
    {
        var payload = _serializer.Deserialize<PriceSchedulePayload>(promotion.PayloadJson);
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
    }

    private static int? ParseLegacyScheduleId(string? code)
    {
        const string prefix = "price-schedule:";
        return code != null && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(code[prefix.Length..], out var id) && id > 0
            ? id
            : null;
    }
}
