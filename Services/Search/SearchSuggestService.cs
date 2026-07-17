// Services/Search/SearchSuggestService.cs
using Fruitables.Data;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Search;

public sealed class SearchSuggestService : ISearchSuggestService
{
    private readonly ApplicationDbContext _db;
    private readonly SearchSuggestOptions _options;
    private readonly IProductPricingService? _pricing;

    public SearchSuggestService(ApplicationDbContext db, IOptions<SearchSuggestOptions> options,
        IProductPricingService? pricing = null)
    {
        _db = db;
        _options = options?.Value ?? new SearchSuggestOptions();
        _pricing = pricing;
    }

    public async Task<SearchSuggestResponse> SuggestAsync(string? query, CancellationToken ct = default)
    {
        var raw = (query ?? string.Empty).Trim();
        if (raw.Length > _options.MaxQueryLength)
            raw = raw[.._options.MaxQueryLength];

        var response = new SearchSuggestResponse
        {
            Query = raw,
            ViewAllUrl = string.IsNullOrEmpty(raw)
                ? "/Shop"
                : "/Shop?search=" + Uri.EscapeDataString(raw)
        };

        if (raw.Length < _options.MinQueryLength)
            return response;

        var qNorm = SearchTextNormalizer.Normalize(raw);
        if (qNorm.Length == 0)
            return response;

        // Shop-scale: load light rows for all active products/categories (no SQL Contains —
        // raw query often lacks diacritics; match is normalize + Score in memory).
        // Images load only for ranked top-N products.
        var productRows = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.Price,
                p.IsFeatured,
                HasVariants = p.Variants.Any(v => v.IsActive)
            })
            .ToListAsync(ct);

        var rankedProducts = productRows
            .Select(p => (p, score: Score(SearchTextNormalizer.Normalize(p.Name), qNorm)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.p.IsFeatured)
            .ThenBy(x => x.p.Name)
            .Take(Math.Max(0, _options.MaxProducts))
            .ToList();

        var topIds = rankedProducts.Select(x => x.p.Id).ToList();
        var imageByProduct = topIds.Count == 0
            ? new Dictionary<int, string?>()
            : await _db.ProductImages.AsNoTracking()
                .Where(i => topIds.Contains(i.ProductId))
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Url = g.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.ProductId, x => (string?)x.Url, ct);

        var quotes = _pricing == null || topIds.Count == 0
            ? new Dictionary<PriceTargetKey, PriceQuote>()
            : (await _pricing.GetQuotesAsync(topIds.Select(id => new PriceTargetKey(id, null))))
                .ToDictionary(x => x.Key, x => x.Value);
        var catalogPrices = _pricing == null || topIds.Count == 0
            ? new Dictionary<int, ProductPriceProjection>()
            : _pricing.ProjectCatalogPrices(_db.Products.Where(p => topIds.Contains(p.Id)))
                .ToDictionary(p => p.ProductId);

        response.Products = rankedProducts
            .Select(x => new SearchSuggestProductDto
            {
                Id = x.p.Id,
                Name = x.p.Name,
                Slug = x.p.Slug,
                Price = x.p.HasVariants && catalogPrices.TryGetValue(x.p.Id, out var range)
                    ? range.MinPrice
                    : x.p.Price,
                SalePrice = !x.p.HasVariants && quotes.TryGetValue(new PriceTargetKey(x.p.Id, null), out var quote) && quote.IsDiscounted
                    ? quote.EffectivePrice
                    : null,
                ImageUrl = imageByProduct.TryGetValue(x.p.Id, out var url) ? url : null,
                Url = "/Shop/Detail/" + Uri.EscapeDataString(x.p.Slug)
            })
            .ToList();

        var categoryRows = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted)
            .Select(c => new { c.Id, c.Name, c.Slug, c.SortOrder })
            .ToListAsync(ct);

        response.Categories = categoryRows
            .Select(c => (c, score: Score(SearchTextNormalizer.Normalize(c.Name), qNorm)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.c.SortOrder)
            .ThenBy(x => x.c.Name)
            .Take(Math.Max(0, _options.MaxCategories))
            .Select(x => new SearchSuggestCategoryDto
            {
                Id = x.c.Id,
                Name = x.c.Name,
                Slug = x.c.Slug,
                Url = "/Shop?categoryId=" + x.c.Id
            })
            .ToList();

        var keywords = await _db.SearchHotKeywords.AsNoTracking()
            .Where(k => k.IsActive)
            .ToListAsync(ct);

        response.Keywords = keywords
            .Select(k =>
            {
                var n = string.IsNullOrEmpty(k.NormalizedText)
                    ? SearchTextNormalizer.Normalize(k.Text)
                    : k.NormalizedText;
                var score = Score(n, qNorm);
                return (k, score);
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.k.Weight)
            .ThenBy(x => x.k.Text)
            .Take(Math.Max(0, _options.MaxKeywords))
            .Select(x => new SearchSuggestKeywordDto
            {
                Text = x.k.Text,
                Url = "/Shop?search=" + Uri.EscapeDataString(x.k.Text)
            })
            .ToList();

        return response;
    }

    /// <summary>2 = prefix, 1 = contains, 0 = no match.</summary>
    internal static int Score(string normalizedDoc, string normalizedQuery)
    {
        if (string.IsNullOrEmpty(normalizedDoc) || string.IsNullOrEmpty(normalizedQuery))
            return 0;
        if (normalizedDoc.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return 2;
        if (normalizedDoc.Contains(normalizedQuery, StringComparison.Ordinal))
            return 1;
        return 0;
    }
}
