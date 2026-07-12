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

    public SearchSuggestService(ApplicationDbContext db, IOptions<SearchSuggestOptions> options)
    {
        _db = db;
        _options = options?.Value ?? new SearchSuggestOptions();
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

        // Coarse filter by Name.Contains(raw) so candidates are bounded by user text,
        // then rank in memory with diacritic-aware normalizer (Score).
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted && p.Name.Contains(raw))
            .Include(p => p.Images)
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.Name)
            .Take(200)
            .ToListAsync(ct);

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted && c.Name.Contains(raw))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Take(100)
            .ToListAsync(ct);

        var keywords = await _db.SearchHotKeywords.AsNoTracking()
            .Where(k => k.IsActive)
            .ToListAsync(ct);

        response.Products = products
            .Select(p =>
            {
                var n = SearchTextNormalizer.Normalize(p.Name);
                var score = Score(n, qNorm);
                return (p, score);
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.p.IsFeatured)
            .ThenBy(x => x.p.Name)
            .Take(Math.Max(0, _options.MaxProducts))
            .Select(x =>
            {
                var img = x.p.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                    ?? x.p.Images?.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl;
                return new SearchSuggestProductDto
                {
                    Id = x.p.Id,
                    Name = x.p.Name,
                    Slug = x.p.Slug,
                    Price = x.p.Price,
                    SalePrice = x.p.SalePrice,
                    ImageUrl = img,
                    Url = "/Shop/Detail/" + Uri.EscapeDataString(x.p.Slug)
                };
            })
            .ToList();

        response.Categories = categories
            .Select(c =>
            {
                var n = SearchTextNormalizer.Normalize(c.Name);
                return (c, score: Score(n, qNorm));
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
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
