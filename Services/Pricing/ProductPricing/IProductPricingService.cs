using Fruitables.ViewModels;
using Fruitables.Models;

namespace Fruitables.Services.Pricing.ProductPricing;

public interface IProductPricingService
{
    Task<PriceQuote?> GetQuoteAsync(int productId, int? variantId = null, DateTimeOffset? at = null);
    Task<IReadOnlyDictionary<PriceTargetKey, PriceQuote>> GetQuotesAsync(IEnumerable<PriceTargetKey> targets, DateTimeOffset? at = null);
    IEnumerable<ProductPriceProjection> ProjectCatalogPrices(IQueryable<Product> products, DateTimeOffset? at = null);
}
