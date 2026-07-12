# Search Suggest Typeahead (Phase 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship storefront typeahead search suggestions (products + categories + hot keywords) on every public search box, backed by a rate-limited JSON API.

**Architecture:** In-process `ISearchSuggestService` inside the existing ASP.NET Core 8 MVC app. Coarse EF queries + in-memory rank after Vietnamese accent-insensitive normalization. Hot keywords from a seeded SQL table. Shared vanilla JS module wires navbar modal, Home, and Shop inputs. No external search engine.

**Tech Stack:** ASP.NET Core 8, EF Core + SQL Server, xUnit + Moq + EF InMemory, Razor + vanilla JS/CSS, existing storefront layout.

**Spec:** `docs/superpowers/specs/2026-07-12-search-suggest-typeahead-design.md`

## Global Constraints

- Product detail URL: `/Shop/Detail/{slug}` (existing `ShopController.Detail`)
- Match: prefix preferred over contains; normalize = trim + lower + strip Vietnamese diacritics + collapse spaces
- Caps: 5 products, 3 categories, 5 keywords (configurable)
- Min query length: 2; max: 50
- Rate limit: 60 req/min/IP
- No personalization, fuzzy typo, or semantic search in P1
- Progressive enhancement: forms still GET `/Shop` without JS
- Follow Fruitables patterns: `Services/` + interfaces, `Controllers/Api/`, `Options/`, tests under `Tests/`
- Prefer small modules; do not bloat `ProductService`

---

## File map

| Path | Responsibility |
|---|---|
| `Services/Search/SearchTextNormalizer.cs` | Canonical match string |
| `Options/SearchSuggestOptions.cs` | Bound `SearchSuggest` config |
| `Models/SearchHotKeyword.cs` | Hot keyword entity |
| `Data/ApplicationDbContext.cs` | DbSet + Fluent + seed |
| `Migrations/*_AddSearchHotKeywords.cs` | EF migration |
| `ViewModels/SearchSuggestViewModels.cs` | API DTOs |
| `Services/Interfaces/ISearchSuggestService.cs` | Suggest abstraction |
| `Services/Search/SearchSuggestService.cs` | Query + rank + URLs |
| `Services/Search/SearchSuggestRateLimitException.cs` | 429 signal |
| `Controllers/Api/SearchSuggestController.cs` | `GET /api/search/suggest` |
| `Program.cs` | DI + options |
| `appsettings.json` | `SearchSuggest` section |
| `wwwroot/js/search-suggest.js` | Debounce, dropdown, keyboard |
| `wwwroot/css/search-suggest.css` | Dropdown styles |
| `Views/Shared/_Layout.cshtml` | CSS + JS includes |
| `Views/Shared/_SearchModal.cshtml` | `data-search-suggest` |
| `Views/Home/Index.cshtml` | `data-search-suggest` on hero inputs |
| `Views/Shop/Index.cshtml` | `data-search-suggest` on shop search |
| `Tests/Search/*` | Unit/service/API tests |

---

### Task 1: SearchTextNormalizer

**Files:**
- Create: `Services/Search/SearchTextNormalizer.cs`
- Test: `Tests/Search/SearchTextNormalizerTests.cs`

**Interfaces:**
- Produces: `SearchTextNormalizer.Normalize(string? text) -> string`

- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/Search/SearchTextNormalizerTests.cs
using Fruitables.Services.Search;
using Xunit;

namespace Fruitables.Tests.Search;

public class SearchTextNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Táo  Fuji  ", "tao fuji")]
    [InlineData("Đậu Hà Lan", "dau ha lan")]
    [InlineData("TRÁI CÂY", "trai cay")]
    [InlineData("ảãáàạăắằẵặâấầẫậ", "aaaaaaaaaaaaaaa")]
    public void Normalize_strips_diacritics_and_collapses_space(string? input, string expected)
    {
        Assert.Equal(expected, SearchTextNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_is_idempotent()
    {
        var once = SearchTextNormalizer.Normalize("Nho Mỹ");
        Assert.Equal(once, SearchTextNormalizer.Normalize(once));
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (type missing)**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SearchTextNormalizerTests" -v n
```

Expected: compile error or fail — `SearchTextNormalizer` not found.

- [ ] **Step 3: Implement normalizer**

```csharp
// Services/Search/SearchTextNormalizer.cs
using System.Globalization;
using System.Text;

namespace Fruitables.Services.Search;

/// <summary>
/// Canonical form for typeahead match: trim, lower, strip Vietnamese diacritics, collapse spaces.
/// </summary>
public static class SearchTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var formD = text.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            // Vietnamese đ/Đ are not decomposed by FormD alone
            if (ch is 'đ' or 'Đ')
            {
                sb.Append('d');
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                    sb.Append(' ');
                continue;
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Trim();
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SearchTextNormalizerTests" -v n
```

- [ ] **Step 5: Commit**

```bash
git add Services/Search/SearchTextNormalizer.cs Tests/Search/SearchTextNormalizerTests.cs
git commit -m "feat(search): add SearchTextNormalizer for typeahead matching"
```

---

### Task 2: Options + DTOs

**Files:**
- Create: `Options/SearchSuggestOptions.cs`
- Create: `ViewModels/SearchSuggestViewModels.cs`
- Modify: `appsettings.json` (add `SearchSuggest` section)

**Interfaces:**
- Produces: `SearchSuggestOptions`, `SearchSuggestResponse`, product/category/keyword DTOs

- [ ] **Step 1: Add options**

```csharp
// Options/SearchSuggestOptions.cs
namespace Fruitables.Options;

public class SearchSuggestOptions
{
    public const string SectionName = "SearchSuggest";

    public int MinQueryLength { get; set; } = 2;
    public int MaxQueryLength { get; set; } = 50;
    public int MaxProducts { get; set; } = 5;
    public int MaxCategories { get; set; } = 3;
    public int MaxKeywords { get; set; } = 5;
    public int RateLimitPerMinute { get; set; } = 60;
}
```

- [ ] **Step 2: Add DTOs**

```csharp
// ViewModels/SearchSuggestViewModels.cs
namespace Fruitables.ViewModels;

public class SearchSuggestResponse
{
    public string Query { get; set; } = string.Empty;
    public List<SearchSuggestProductDto> Products { get; set; } = new();
    public List<SearchSuggestCategoryDto> Categories { get; set; } = new();
    public List<SearchSuggestKeywordDto> Keywords { get; set; } = new();
    public string ViewAllUrl { get; set; } = string.Empty;
}

public class SearchSuggestProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class SearchSuggestCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class SearchSuggestKeywordDto
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Add appsettings section** (merge into existing JSON root)

```json
"SearchSuggest": {
  "MinQueryLength": 2,
  "MaxQueryLength": 50,
  "MaxProducts": 5,
  "MaxCategories": 3,
  "MaxKeywords": 5,
  "RateLimitPerMinute": 60
}
```

- [ ] **Step 4: Commit**

```bash
git add Options/SearchSuggestOptions.cs ViewModels/SearchSuggestViewModels.cs appsettings.json
git commit -m "feat(search): add SearchSuggest options and DTOs"
```

---

### Task 3: SearchHotKeyword entity + DbContext + migration + seed

**Files:**
- Create: `Models/SearchHotKeyword.cs`
- Modify: `Data/ApplicationDbContext.cs`
- Create: EF migration `AddSearchHotKeywords`
- Test: `Tests/Search/SearchHotKeywordEntityTests.cs`

**Interfaces:**
- Produces: `DbSet<SearchHotKeyword>`, seeded rows with `NormalizedText`

- [ ] **Step 1: Write entity persistence test**

```csharp
// Tests/Search/SearchHotKeywordEntityTests.cs
using Fruitables.Data;
using Fruitables.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests.Search;

public class SearchHotKeywordEntityTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Can_persist_hot_keyword()
    {
        await using var db = CreateContext();
        db.SearchHotKeywords.Add(new SearchHotKeyword
        {
            Text = "Táo Fuji",
            NormalizedText = "tao fuji",
            Weight = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var row = await db.SearchHotKeywords.SingleAsync();
        Assert.Equal("Táo Fuji", row.Text);
        Assert.Equal("tao fuji", row.NormalizedText);
        Assert.True(row.IsActive);
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (DbSet missing)

- [ ] **Step 3: Implement model**

```csharp
// Models/SearchHotKeyword.cs
using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class SearchHotKeyword
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Text { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string NormalizedText { get; set; } = string.Empty;

    /// <summary>Higher = preferred within keyword group.</summary>
    public int Weight { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Wire DbContext**

In `ApplicationDbContext.cs`:

```csharp
public DbSet<SearchHotKeyword> SearchHotKeywords => Set<SearchHotKeyword>();
```

In `OnModelCreating`, add:

```csharp
modelBuilder.Entity<SearchHotKeyword>(entity =>
{
    entity.HasIndex(e => e.NormalizedText);
    entity.HasIndex(e => e.IsActive);

    var seedAt = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    entity.HasData(
        new SearchHotKeyword { Id = 1, Text = "táo", NormalizedText = "tao", Weight = 100, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 2, Text = "cam", NormalizedText = "cam", Weight = 90, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 3, Text = "nho", NormalizedText = "nho", Weight = 80, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 4, Text = "dâu", NormalizedText = "dau", Weight = 80, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 5, Text = "rau củ", NormalizedText = "rau cu", Weight = 95, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 6, Text = "trái cây", NormalizedText = "trai cay", Weight = 95, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 7, Text = "combo", NormalizedText = "combo", Weight = 85, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 8, Text = "táo fuji", NormalizedText = "tao fuji", Weight = 70, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 9, Text = "chuối", NormalizedText = "chuoi", Weight = 70, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 10, Text = "bơ", NormalizedText = "bo", Weight = 70, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 11, Text = "xoài", NormalizedText = "xoai", Weight = 70, IsActive = true, CreatedAt = seedAt },
        new SearchHotKeyword { Id = 12, Text = "nước ép", NormalizedText = "nuoc ep", Weight = 60, IsActive = true, CreatedAt = seedAt }
    );
});
```

- [ ] **Step 5: Migration**

```bash
dotnet ef migrations add AddSearchHotKeywords --project Fruitables.csproj
```

- [ ] **Step 6: Run entity test — PASS**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SearchHotKeywordEntityTests" -v n
```

- [ ] **Step 7: Commit**

```bash
git add Models/SearchHotKeyword.cs Data/ApplicationDbContext.cs Migrations/ Tests/Search/SearchHotKeywordEntityTests.cs
git commit -m "feat(search): add SearchHotKeyword entity, seed, and migration"
```

---

### Task 4: SearchSuggestService (core ranking)

**Files:**
- Create: `Services/Interfaces/ISearchSuggestService.cs`
- Create: `Services/Search/SearchSuggestService.cs`
- Test: `Tests/Search/SearchSuggestServiceTests.cs`

**Interfaces:**
- Consumes: `ApplicationDbContext`, `IOptions<SearchSuggestOptions>`, `SearchTextNormalizer`
- Produces: `Task<SearchSuggestResponse> SuggestAsync(string? query, CancellationToken ct = default)`

- [ ] **Step 1: Write failing service tests**

```csharp
// Tests/Search/SearchSuggestServiceTests.cs
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fruitables.Tests.Search;

public class SearchSuggestServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static SearchSuggestService CreateSut(ApplicationDbContext db, SearchSuggestOptions? opt = null)
    {
        return new SearchSuggestService(
            db,
            Options.Create(opt ?? new SearchSuggestOptions()));
    }

    private static async Task SeedCatalogAsync(ApplicationDbContext db)
    {
        var catFruit = new Category
        {
            Id = 1, Name = "Trái cây", Slug = "trai-cay", IsActive = true, IsDeleted = false
        };
        var catVeg = new Category
        {
            Id = 2, Name = "Rau củ", Slug = "rau-cu", IsActive = true, IsDeleted = false
        };
        db.Categories.AddRange(catFruit, catVeg);

        db.Products.AddRange(
            new Product
            {
                Id = 1, CategoryId = 1, Name = "Táo Fuji", Slug = "tao-fuji",
                Price = 125000, SalePrice = 99000, IsActive = true, IsDeleted = false, IsFeatured = true
            },
            new Product
            {
                Id = 2, CategoryId = 1, Name = "Cam sành", Slug = "cam-sanh",
                Price = 45000, IsActive = true, IsDeleted = false, IsFeatured = false
            },
            new Product
            {
                Id = 3, CategoryId = 1, Name = "Nho Mỹ", Slug = "nho-my",
                Price = 150000, IsActive = false, IsDeleted = false
            },
            new Product
            {
                Id = 4, CategoryId = 2, Name = "Cà rốt", Slug = "ca-rot",
                Price = 20000, IsActive = true, IsDeleted = true
            });

        db.ProductImages.Add(new ProductImage
        {
            ProductId = 1, ImageUrl = "/uploads/tao.jpg", IsPrimary = true, SortOrder = 0
        });

        db.SearchHotKeywords.AddRange(
            new SearchHotKeyword { Text = "táo fuji", NormalizedText = "tao fuji", Weight = 50, IsActive = true },
            new SearchHotKeyword { Text = "combo", NormalizedText = "combo", Weight = 10, IsActive = true },
            new SearchHotKeyword { Text = "hidden", NormalizedText = "hidden", Weight = 99, IsActive = false });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Suggest_short_query_returns_empty_groups()
    {
        await using var db = CreateContext();
        await SeedCatalogAsync(db);
        var sut = CreateSut(db);

        var result = await sut.SuggestAsync("t");

        Assert.Empty(result.Products);
        Assert.Empty(result.Categories);
        Assert.Empty(result.Keywords);
        Assert.Equal("t", result.Query);
    }

    [Fact]
    public async Task Suggest_matches_product_category_keyword_and_ranks_prefix()
    {
        await using var db = CreateContext();
        await SeedCatalogAsync(db);
        var sut = CreateSut(db);

        var result = await sut.SuggestAsync("Táo");

        Assert.Contains(result.Products, p => p.Slug == "tao-fuji");
        Assert.DoesNotContain(result.Products, p => p.Slug == "nho-my"); // inactive
        Assert.DoesNotContain(result.Products, p => p.Slug == "ca-rot"); // deleted
        Assert.Equal("/Shop/Detail/tao-fuji", result.Products.First(p => p.Slug == "tao-fuji").Url);
        Assert.Equal("/uploads/tao.jpg", result.Products.First(p => p.Slug == "tao-fuji").ImageUrl);
        Assert.Contains(result.Keywords, k => k.Text == "táo fuji");
        Assert.DoesNotContain(result.Keywords, k => k.Text == "hidden");
        Assert.StartsWith("/Shop?search=", result.ViewAllUrl);
    }

    [Fact]
    public async Task Suggest_category_match_on_trai_cay()
    {
        await using var db = CreateContext();
        await SeedCatalogAsync(db);
        var sut = CreateSut(db);

        var result = await sut.SuggestAsync("trai");

        Assert.Contains(result.Categories, c => c.Slug == "trai-cay" && c.Url == "/Shop?categoryId=1");
    }

    [Fact]
    public async Task Suggest_respects_max_products_cap()
    {
        await using var db = CreateContext();
        for (var i = 1; i <= 10; i++)
        {
            db.Products.Add(new Product
            {
                Id = i, CategoryId = 1, Name = $"Táo số {i}",
                Slug = $"tao-{i}", Price = 1000, IsActive = true, IsDeleted = false
            });
        }
        // Category required? If FK enforced in InMemory may need category row:
        db.Categories.Add(new Category { Id = 1, Name = "Trái cây", Slug = "trai-cay", IsActive = true });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new SearchSuggestOptions { MaxProducts = 5, MinQueryLength = 2 });
        var result = await sut.SuggestAsync("tao");
        Assert.True(result.Products.Count <= 5);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement interface + service**

```csharp
// Services/Interfaces/ISearchSuggestService.cs
using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface ISearchSuggestService
{
    Task<SearchSuggestResponse> SuggestAsync(string? query, CancellationToken ct = default);
}
```

```csharp
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

        // Coarse candidates: load a bounded set (shop-scale) then rank in memory
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .Include(p => p.Images)
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.Name)
            .Take(200)
            .ToListAsync(ct);

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted)
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
```

- [ ] **Step 4: Run service tests — PASS**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SearchSuggestServiceTests" -v n
```

If InMemory FK complains about `CategoryId`, ensure category rows exist before products (adjust seed order as in cap test).

- [ ] **Step 5: Commit**

```bash
git add Services/Interfaces/ISearchSuggestService.cs Services/Search/SearchSuggestService.cs Tests/Search/SearchSuggestServiceTests.cs
git commit -m "feat(search): add SearchSuggestService with hybrid rank"
```

---

### Task 5: API controller + rate limit + DI

**Files:**
- Create: `Services/Search/SearchSuggestRateLimitException.cs`
- Create: `Controllers/Api/SearchSuggestController.cs`
- Modify: `Program.cs`
- Test: `Tests/Search/SearchSuggestControllerTests.cs`

**Interfaces:**
- Consumes: `ISearchSuggestService`, `IMemoryCache`, `IOptions<SearchSuggestOptions>`
- Produces: `GET /api/search/suggest?q=`

- [ ] **Step 1: Rate limit exception**

```csharp
// Services/Search/SearchSuggestRateLimitException.cs
namespace Fruitables.Services.Search;

public sealed class SearchSuggestRateLimitException : Exception
{
    public SearchSuggestRateLimitException(string message) : base(message) { }
}
```

- [ ] **Step 2: Controller tests (Moq)**

```csharp
// Tests/Search/SearchSuggestControllerTests.cs
using Fruitables.Controllers.Api;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Search;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Fruitables.Tests.Search;

public class SearchSuggestControllerTests
{
    private static SearchSuggestController Create(
        Mock<ISearchSuggestService> svc,
        SearchSuggestOptions? opt = null,
        IMemoryCache? cache = null)
    {
        var controller = new SearchSuggestController(
            svc.Object,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Options.Create(opt ?? new SearchSuggestOptions { RateLimitPerMinute = 60 }),
            NullLogger<SearchSuggestController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10") }
            }
        };
        return controller;
    }

    [Fact]
    public async Task Suggest_returns_ok_payload()
    {
        var svc = new Mock<ISearchSuggestService>();
        svc.Setup(s => s.SuggestAsync("tao", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSuggestResponse
            {
                Query = "tao",
                ViewAllUrl = "/Shop?search=tao",
                Products = new List<SearchSuggestProductDto>
                {
                    new() { Id = 1, Name = "Táo", Slug = "tao", Url = "/Shop/Detail/tao" }
                }
            });

        var result = await Create(svc).Suggest("tao", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SearchSuggestResponse>(ok.Value);
        Assert.Single(body.Products);
    }

    [Fact]
    public async Task Suggest_rate_limit_returns_429()
    {
        var svc = new Mock<ISearchSuggestService>();
        svc.Setup(s => s.SuggestAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSuggestResponse());

        var opt = new SearchSuggestOptions { RateLimitPerMinute = 2 };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = Create(svc, opt, cache);

        Assert.IsType<OkObjectResult>(await controller.Suggest("ab", CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.Suggest("ab", CancellationToken.None));
        var limited = await controller.Suggest("ab", CancellationToken.None);
        var status = Assert.IsType<ObjectResult>(limited);
        Assert.Equal(429, status.StatusCode);
        svc.Verify(s => s.SuggestAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
```

- [ ] **Step 3: Implement controller**

```csharp
// Controllers/Api/SearchSuggestController.cs
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Fruitables.Controllers.Api;

[ApiController]
[Route("api/search")]
public class SearchSuggestController : ControllerBase
{
    private readonly ISearchSuggestService _service;
    private readonly IMemoryCache _cache;
    private readonly SearchSuggestOptions _options;
    private readonly ILogger<SearchSuggestController> _logger;

    public SearchSuggestController(
        ISearchSuggestService service,
        IMemoryCache cache,
        IOptions<SearchSuggestOptions> options,
        ILogger<SearchSuggestController> logger)
    {
        _service = service;
        _cache = cache;
        _options = options?.Value ?? new SearchSuggestOptions();
        _logger = logger;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken ct)
    {
        try
        {
            EnforceRateLimit();
            var payload = await _service.SuggestAsync(q, ct);
            return Ok(payload);
        }
        catch (SearchSuggestRateLimitException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search suggest failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau."
            });
        }
    }

    private void EnforceRateLimit()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var bucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var key = $"search-suggest-rl:{ip}:{bucket}";

        var count = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return 0;
        });

        count++;
        _cache.Set(key, count, TimeSpan.FromMinutes(2));

        if (count > _options.RateLimitPerMinute)
        {
            throw new SearchSuggestRateLimitException(
                $"Rate limit exceeded. Maximum {_options.RateLimitPerMinute} requests per minute.");
        }
    }
}
```

- [ ] **Step 4: Register DI in `Program.cs`** (near other service registrations)

```csharp
builder.Services.Configure<SearchSuggestOptions>(
    builder.Configuration.GetSection(SearchSuggestOptions.SectionName));
builder.Services.AddScoped<ISearchSuggestService, SearchSuggestService>();
// IMemoryCache already registered in most ASP.NET apps; if missing:
// builder.Services.AddMemoryCache();
```

Add usings:

```csharp
using Fruitables.Options;
using Fruitables.Services.Search;
using Fruitables.Services.Interfaces;
```

Confirm `AddMemoryCache()` exists (chat rate limit already needs it — verify once).

- [ ] **Step 5: Run controller tests — PASS**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SearchSuggest" -v n
```

- [ ] **Step 6: Commit**

```bash
git add Controllers/Api/SearchSuggestController.cs Services/Search/SearchSuggestRateLimitException.cs Program.cs Tests/Search/SearchSuggestControllerTests.cs
git commit -m "feat(search): add /api/search/suggest endpoint with rate limit"
```

---

### Task 6: CSS + JS typeahead client

**Files:**
- Create: `wwwroot/css/search-suggest.css`
- Create: `wwwroot/js/search-suggest.js`

**Interfaces:**
- Consumes: `GET /api/search/suggest?q=`
- Produces: dropdown UI bound to `[data-search-suggest]`

- [ ] **Step 1: CSS**

```css
/* wwwroot/css/search-suggest.css */
.search-suggest-wrap {
    position: relative;
}

.search-suggest-dropdown {
    position: absolute;
    left: 0;
    right: 0;
    top: calc(100% + 4px);
    z-index: 1080;
    max-height: min(70vh, 420px);
    overflow-y: auto;
    background: #fff;
    border: 1px solid #e5e7eb;
    border-radius: 12px;
    box-shadow: 0 12px 32px rgba(15, 23, 42, 0.12);
    text-align: left;
}

.search-suggest-group-title {
    padding: 8px 14px 4px;
    font-size: 0.75rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: #64748b;
}

.search-suggest-item {
    display: flex;
    align-items: center;
    gap: 10px;
    width: 100%;
    padding: 10px 14px;
    border: 0;
    background: transparent;
    text-decoration: none;
    color: #0f172a;
    cursor: pointer;
}

.search-suggest-item:hover,
.search-suggest-item.is-active {
    background: #f0fdf4;
    color: #0f172a;
}

.search-suggest-thumb {
    width: 40px;
    height: 40px;
    object-fit: cover;
    border-radius: 8px;
    background: #f1f5f9;
    flex-shrink: 0;
}

.search-suggest-thumb-placeholder {
    width: 40px;
    height: 40px;
    border-radius: 8px;
    background: #f1f5f9;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #94a3b8;
    flex-shrink: 0;
}

.search-suggest-meta {
    min-width: 0;
    flex: 1;
}

.search-suggest-name {
    display: block;
    font-weight: 600;
    font-size: 0.92rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.search-suggest-price {
    font-size: 0.85rem;
    color: #81c408;
    font-weight: 600;
}

.search-suggest-price del {
    color: #94a3b8;
    font-weight: 400;
    margin-right: 6px;
}

.search-suggest-view-all {
    display: block;
    padding: 12px 14px;
    border-top: 1px solid #e5e7eb;
    font-weight: 600;
    color: #81c408;
    text-decoration: none;
}

.search-suggest-view-all:hover,
.search-suggest-view-all.is-active {
    background: #f8fafc;
}

.search-suggest-empty {
    padding: 12px 14px;
    color: #64748b;
    font-size: 0.9rem;
}

/* Fullscreen search modal: keep dropdown usable */
#searchModal .search-suggest-dropdown {
    z-index: 1090;
}
```

- [ ] **Step 2: JS module** (complete, self-contained IIFE)

```javascript
// wwwroot/js/search-suggest.js
(function () {
    'use strict';

    if (window.__fruitablesSearchSuggestInit) return;
    window.__fruitablesSearchSuggestInit = true;

    var DEBOUNCE_MS = 250;
    var MIN_LEN = 2;

    function escapeHtml(text) {
        var d = document.createElement('div');
        d.textContent = text == null ? '' : String(text);
        return d.innerHTML;
    }

    function formatPrice(n) {
        try {
            return new Intl.NumberFormat('vi-VN').format(Number(n)) + 'đ';
        } catch (e) {
            return String(n) + 'đ';
        }
    }

    function ensureWrap(input) {
        var parent = input.parentElement;
        if (!parent) return null;
        if (parent.classList.contains('search-suggest-wrap')) return parent;
        // Prefer wrapping just the input when parent is input-group: insert wrap around input
        var wrap = document.createElement('div');
        wrap.className = 'search-suggest-wrap flex-grow-1';
        parent.insertBefore(wrap, input);
        wrap.appendChild(input);
        return wrap;
    }

    function closeDropdown(state) {
        if (state.dropdown && state.dropdown.parentNode) {
            state.dropdown.remove();
        }
        state.dropdown = null;
        state.items = [];
        state.activeIndex = -1;
        inputAria(state.input, false);
    }

    function inputAria(input, expanded) {
        input.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-autocomplete', 'list');
    }

    function setActive(state, index) {
        state.activeIndex = index;
        if (!state.dropdown) return;
        var nodes = state.dropdown.querySelectorAll('[data-suggest-index]');
        for (var i = 0; i < nodes.length; i++) {
            nodes[i].classList.toggle('is-active', i === index);
        }
        if (index >= 0 && nodes[index]) {
            state.input.setAttribute('aria-activedescendant', nodes[index].id);
            nodes[index].scrollIntoView({ block: 'nearest' });
        } else {
            state.input.removeAttribute('aria-activedescendant');
        }
    }

    function render(state, data) {
        closeDropdown(state);
        var wrap = ensureWrap(state.input);
        if (!wrap) return;

        var dd = document.createElement('div');
        dd.className = 'search-suggest-dropdown';
        dd.setAttribute('role', 'listbox');
        dd.id = 'search-suggest-list-' + state.uid;

        var items = [];
        var html = '';

        function pushItem(kind, labelHtml, href, extraClass) {
            var idx = items.length;
            var id = 'ssi-' + state.uid + '-' + idx;
            items.push({ href: href });
            html +=
                '<a href="' +
                escapeHtml(href) +
                '" class="search-suggest-item ' +
                (extraClass || '') +
                '" role="option" id="' +
                id +
                '" data-suggest-index="' +
                idx +
                '">' +
                labelHtml +
                '</a>';
        }

        var products = data.products || data.Products || [];
        var categories = data.categories || data.Categories || [];
        var keywords = data.keywords || data.Keywords || [];
        var viewAll = data.viewAllUrl || data.ViewAllUrl || '/Shop';
        var q = data.query || data.Query || state.input.value || '';

        if (products.length) {
            html += '<div class="search-suggest-group-title">Sản phẩm</div>';
            products.forEach(function (p) {
                var name = p.name || p.Name || '';
                var url = p.url || p.Url || '#';
                var img = p.imageUrl || p.ImageUrl;
                var price = p.salePrice != null ? p.salePrice : p.SalePrice != null ? p.SalePrice : p.price != null ? p.price : p.Price;
                var listPrice = p.price != null ? p.price : p.Price;
                var sale = p.salePrice != null ? p.salePrice : p.SalePrice;
                var thumb = img
                    ? '<img class="search-suggest-thumb" src="' + escapeHtml(img) + '" alt="">'
                    : '<span class="search-suggest-thumb-placeholder"><i class="fa fa-leaf"></i></span>';
                var priceHtml = '';
                if (sale != null && listPrice != null && Number(sale) < Number(listPrice)) {
                    priceHtml =
                        '<span class="search-suggest-price"><del>' +
                        escapeHtml(formatPrice(listPrice)) +
                        '</del>' +
                        escapeHtml(formatPrice(sale)) +
                        '</span>';
                } else if (price != null) {
                    priceHtml =
                        '<span class="search-suggest-price">' +
                        escapeHtml(formatPrice(price)) +
                        '</span>';
                }
                pushItem(
                    'product',
                    thumb +
                        '<span class="search-suggest-meta"><span class="search-suggest-name">' +
                        escapeHtml(name) +
                        '</span>' +
                        priceHtml +
                        '</span>',
                    url
                );
            });
        }

        if (categories.length) {
            html += '<div class="search-suggest-group-title">Danh mục</div>';
            categories.forEach(function (c) {
                var name = c.name || c.Name || '';
                var url = c.url || c.Url || '#';
                pushItem(
                    'cat',
                    '<span class="search-suggest-thumb-placeholder"><i class="fa fa-folder"></i></span>' +
                        '<span class="search-suggest-meta"><span class="search-suggest-name">' +
                        escapeHtml(name) +
                        '</span></span>',
                    url
                );
            });
        }

        if (keywords.length) {
            html += '<div class="search-suggest-group-title">Gợi ý</div>';
            keywords.forEach(function (k) {
                var text = k.text || k.Text || '';
                var url = k.url || k.Url || '#';
                pushItem(
                    'kw',
                    '<span class="search-suggest-thumb-placeholder"><i class="fa fa-search"></i></span>' +
                        '<span class="search-suggest-meta"><span class="search-suggest-name">' +
                        escapeHtml(text) +
                        '</span></span>',
                    url
                );
            });
        }

        if (!products.length && !categories.length && !keywords.length) {
            html +=
                '<div class="search-suggest-empty">Không có gợi ý phù hợp</div>';
        }

        var viewIdx = items.length;
        items.push({ href: viewAll });
        html +=
            '<a href="' +
            escapeHtml(viewAll) +
            '" class="search-suggest-view-all" role="option" id="ssi-' +
            state.uid +
            '-' +
            viewIdx +
            '" data-suggest-index="' +
            viewIdx +
            '">Xem tất cả kết quả cho “' +
            escapeHtml(q) +
            '”</a>';

        dd.innerHTML = html;
        wrap.appendChild(dd);
        state.dropdown = dd;
        state.items = items;
        state.activeIndex = -1;
        inputAria(state.input, true);

        dd.addEventListener('mousedown', function (e) {
            // prevent input blur before navigation
            e.preventDefault();
        });
    }

    function fetchSuggest(state, q) {
        var seq = ++state.seq;
        fetch('/api/search/suggest?q=' + encodeURIComponent(q), {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        })
            .then(function (res) {
                if (!res.ok) throw new Error('suggest failed');
                return res.json();
            })
            .then(function (data) {
                if (seq !== state.seq) return;
                if ((state.input.value || '').trim() !== q) return;
                render(state, data || {});
            })
            .catch(function () {
                if (seq !== state.seq) return;
                closeDropdown(state);
            });
    }

    function onInput(state) {
        var q = (state.input.value || '').trim();
        if (q.length < MIN_LEN) {
            closeDropdown(state);
            return;
        }
        clearTimeout(state.timer);
        state.timer = setTimeout(function () {
            fetchSuggest(state, q);
        }, DEBOUNCE_MS);
    }

    function onKeyDown(state, e) {
        if (!state.dropdown) {
            if (e.key === 'ArrowDown' && (state.input.value || '').trim().length >= MIN_LEN) {
                onInput(state);
            }
            return;
        }
        if (e.key === 'Escape') {
            e.preventDefault();
            closeDropdown(state);
            return;
        }
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            var next = state.activeIndex + 1;
            if (next >= state.items.length) next = 0;
            setActive(state, next);
            return;
        }
        if (e.key === 'ArrowUp') {
            e.preventDefault();
            var prev = state.activeIndex - 1;
            if (prev < 0) prev = state.items.length - 1;
            setActive(state, prev);
            return;
        }
        if (e.key === 'Enter' && state.activeIndex >= 0 && state.items[state.activeIndex]) {
            e.preventDefault();
            window.location.href = state.items[state.activeIndex].href;
        }
        // Enter with no selection → let form submit (Shop search)
    }

    function bindInput(input, index) {
        if (input.dataset.searchSuggestBound === '1') return;
        input.dataset.searchSuggestBound = '1';
        inputAria(input, false);

        var state = {
            input: input,
            uid: String(index) + '-' + Math.random().toString(36).slice(2, 7),
            timer: null,
            seq: 0,
            dropdown: null,
            items: [],
            activeIndex: -1
        };

        input.addEventListener('input', function () {
            onInput(state);
        });
        input.addEventListener('keydown', function (e) {
            onKeyDown(state, e);
        });
        input.addEventListener('blur', function () {
            setTimeout(function () {
                closeDropdown(state);
            }, 150);
        });
        input.addEventListener('focus', function () {
            var q = (input.value || '').trim();
            if (q.length >= MIN_LEN) onInput(state);
        });
    }

    function boot() {
        document.querySelectorAll('[data-search-suggest]').forEach(function (el, i) {
            bindInput(el, i);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
```

- [ ] **Step 3: Commit**

```bash
git add wwwroot/css/search-suggest.css wwwroot/js/search-suggest.js
git commit -m "feat(search): add typeahead client assets"
```

---

### Task 7: Wire views + layout assets

**Files:**
- Modify: `Views/Shared/_Layout.cshtml`
- Modify: `Views/Shared/_SearchModal.cshtml`
- Modify: `Views/Home/Index.cshtml`
- Modify: `Views/Shop/Index.cshtml`

- [ ] **Step 1: Layout CSS/JS**

Near other CSS (`</head>` area or existing stylesheet block):

```html
<link rel="stylesheet" href="~/css/search-suggest.css" asp-append-version="true" />
```

Near `chat.js` (before `</body>` scripts):

```html
<script src="~/js/search-suggest.js" asp-append-version="true"></script>
```

- [ ] **Step 2: Mark inputs**

`_SearchModal.cshtml` — on the search input:

```html
<input type="search" name="search" data-search-suggest class="form-control p-3" placeholder="Nhập tên sản phẩm..." aria-describedby="search-icon-1">
```

`Home/Index.cshtml` — each hero search input (`name="search"`):

```html
<input name="search" type="search" data-search-suggest class="form-control" placeholder="..." />
```

(Keep existing placeholders; only add `data-search-suggest`.)

`Shop/Index.cshtml`:

```html
<input id="shopSearch" type="search" name="search" data-search-suggest class="form-control" placeholder="Tìm rau củ, trái cây, combo..." value="@Model.SearchTerm" />
```

- [ ] **Step 3: Manual smoke checklist**

1. Apply migration if not applied: `dotnet ef database update --project Fruitables.csproj`
2. Run app: `dotnet run --project Fruitables.csproj`
3. Open Home — type `tao` in hero search → products/keywords appear
4. Navbar search modal — same
5. Shop search — same
6. Click product → `/Shop/Detail/{slug}`
7. Click category → `/Shop?categoryId=`
8. Click keyword / view-all → Shop search
9. Arrow keys + Enter
10. Disable JS (or block script) → form submit still works

- [ ] **Step 4: Full test suite for Search**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~Search" -v n
```

Expected: all Search tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Views/Shared/_Layout.cshtml Views/Shared/_SearchModal.cshtml Views/Home/Index.cshtml Views/Shop/Index.cshtml
git commit -m "feat(search): wire typeahead on all storefront search inputs"
```

---

## Spec coverage checklist (self-review)

| Spec requirement | Task |
|---|---|
| Typeahead all storefront boxes | 6–7 |
| Products + categories + keywords | 4 |
| Click targets + view-all | 4 (URLs), 6 (UI) |
| Normalize accent-insensitive | 1, 4 |
| Catalog + hot keywords seed | 3–4 |
| Rate limit public API | 5 |
| XSS escape client | 6 |
| Progressive enhancement | 7 (forms unchanged) |
| Options caps / min length | 2, 4 |
| Tests normalizer/service/API | 1, 4, 5 |
| No admin CRUD / personalization / FTS | omitted by design |

**Placeholder scan:** none intentional.  
**Type consistency:** `SearchSuggestResponse` / DTO names stable across Tasks 2–6; product URL `/Shop/Detail/{slug}`.

---

## Execution handoff

Plan saved to `docs/superpowers/plans/2026-07-12-search-suggest-typeahead.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — execute tasks in this session with checkpoints  

Which approach?
