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
            Microsoft.Extensions.Options.Options.Create(opt ?? new SearchSuggestOptions()));
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

        // Accent-insensitive: query without diacritics still matches "Trái cây"
        var result = await sut.SuggestAsync("trai");

        Assert.Contains(result.Categories, c => c.Slug == "trai-cay" && c.Url == "/Shop?categoryId=1");
    }

    [Fact]
    public async Task Suggest_unaccented_query_matches_accented_product_name()
    {
        await using var db = CreateContext();
        await SeedCatalogAsync(db);
        var sut = CreateSut(db);

        var result = await sut.SuggestAsync("tao");

        Assert.Contains(result.Products, p => p.Slug == "tao-fuji");
        Assert.Contains(result.Keywords, k => k.Text == "táo fuji");
    }

    [Fact]
    public async Task Suggest_respects_max_products_cap()
    {
        await using var db = CreateContext();
        // Category first so FK is valid if InMemory enforces it
        db.Categories.Add(new Category { Id = 1, Name = "Trái cây", Slug = "trai-cay", IsActive = true });
        for (var i = 1; i <= 10; i++)
        {
            db.Products.Add(new Product
            {
                Id = i, CategoryId = 1, Name = $"Táo số {i}",
                Slug = $"tao-{i}", Price = 1000, IsActive = true, IsDeleted = false
            });
        }
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new SearchSuggestOptions { MaxProducts = 5, MinQueryLength = 2 });
        // Query must match Name.Contains (InMemory is case/diacritic-sensitive)
        var result = await sut.SuggestAsync("Táo");
        Assert.True(result.Products.Count <= 5);
        Assert.Equal(5, result.Products.Count);
    }

    [Fact]
    public async Task Suggest_prefix_product_ranks_above_contains()
    {
        await using var db = CreateContext();
        db.Categories.Add(new Category { Id = 1, Name = "Trái cây", Slug = "trai-cay", IsActive = true });
        db.Products.AddRange(
            new Product
            {
                Id = 1, CategoryId = 1, Name = "Hộp quà Táo", Slug = "hop-qua-tao",
                Price = 1000, IsActive = true, IsDeleted = false, IsFeatured = false
            },
            new Product
            {
                Id = 2, CategoryId = 1, Name = "Táo đỏ", Slug = "tao-do",
                Price = 1000, IsActive = true, IsDeleted = false, IsFeatured = false
            });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.SuggestAsync("Táo");

        Assert.Equal(2, result.Products.Count);
        Assert.Equal("tao-do", result.Products[0].Slug);
        Assert.Equal("hop-qua-tao", result.Products[1].Slug);
    }
}
