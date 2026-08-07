using System.Security.Cryptography;
using System.Text;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Chat.Providers;
using Fruitables.Services.Communications;
using Fruitables.Tests.Chat.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fruitables.Tests.Chat;

public class IndexingServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IndexingService CreateService(
        ApplicationDbContext db,
        IEmbeddingClient embeddingClient)
    {
        return new IndexingService(
            db,
            embeddingClient,
            Microsoft.Extensions.Options.Options.Create(new ChatOptions()),
            NullLogger<IndexingService>.Instance);
    }

    private static string Sha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [Fact]
    public async Task IndexFaqAsync_creates_chunk()
    {
        await using var db = CreateContext();
        var serializer = new Fruitables.Services.Infrastructure.Json.VersionedJsonSerializer();
        var entry = Fruitables.Services.Infrastructure.Content.ContentEntryMapper.FromFaq(new Faq
        {
            Title = "Phí ship",
            Body = "Nội thành 30k",
            Category = "shipping",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, serializer);
        db.ContentEntries.Add(entry);
        await db.SaveChangesAsync();
        entry.Key = Fruitables.Services.Infrastructure.Content.ContentEntryMapper.Key("faq", entry.Id);
        await db.SaveChangesAsync();

        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var sut = CreateService(db, embedding);

        await sut.IndexFaqAsync(entry.Id);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == entry.Id.ToString())
            .ToListAsync();

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.True(chunk.IsActive);
        Assert.Equal("Phí ship", chunk.Title);
        Assert.StartsWith("Phí ship\n\nNội thành 30k", chunk.Content);
        Assert.Contains("Từ khóa:", chunk.Content);
        Assert.Contains("ship", chunk.Content, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(chunk.ContentHash));
        Assert.False(string.IsNullOrWhiteSpace(chunk.EmbeddingJson));
        Assert.NotEqual("[]", chunk.EmbeddingJson);
        // Hash gắn AlgorithmId để bump algorithm → reindex embedding
        Assert.Equal(
            Sha256Hex(RetrievalText.AlgorithmId + "\0" + chunk.Content),
            chunk.ContentHash);
    }

    [Fact]
    public async Task IndexFaqAsync_inactive_disables_chunks()
    {
        await using var db = CreateContext();
        var serializer = new Fruitables.Services.Infrastructure.Json.VersionedJsonSerializer();
        var entry = Fruitables.Services.Infrastructure.Content.ContentEntryMapper.FromFaq(new Faq
        {
            Title = "Hỗ trợ đơn hàng",
            Body = "Liên hệ cửa hàng để được hỗ trợ",
            Category = "support",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, serializer);
        db.ContentEntries.Add(entry);
        await db.SaveChangesAsync();
        entry.Key = Fruitables.Services.Infrastructure.Content.ContentEntryMapper.Key("faq", entry.Id);
        await db.SaveChangesAsync();

        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var sut = CreateService(db, embedding);

        await sut.IndexFaqAsync(entry.Id);
        Assert.True(await db.KnowledgeChunks.AnyAsync(c => c.IsActive));

        entry.IsActive = false;
        await db.SaveChangesAsync();

        await sut.IndexFaqAsync(entry.Id);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == entry.Id.ToString())
            .ToListAsync();

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.False(c.IsActive));
    }

    [Fact]
    public async Task IndexFaqAsync_same_content_skips_reembed()
    {
        await using var db = CreateContext();
        var serializer = new Fruitables.Services.Infrastructure.Json.VersionedJsonSerializer();
        var entry = Fruitables.Services.Infrastructure.Content.ContentEntryMapper.FromFaq(new Faq
        {
            Title = "Giờ mở cửa",
            Body = "8h-20h mỗi ngày",
            Category = "general",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, serializer);
        db.ContentEntries.Add(entry);
        await db.SaveChangesAsync();
        entry.Key = Fruitables.Services.Infrastructure.Content.ContentEntryMapper.Key("faq", entry.Id);
        await db.SaveChangesAsync();

        var inner = new DeterministicEmbeddingClient(dimensions: 32);
        var counting = new CountingEmbeddingClient(inner);
        var sut = CreateService(db, counting);

        await sut.IndexFaqAsync(entry.Id);
        Assert.Equal(1, counting.EmbedCallCount);

        await sut.IndexFaqAsync(entry.Id);
        Assert.Equal(1, counting.EmbedCallCount);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == entry.Id.ToString() && c.IsActive)
            .ToListAsync();
        Assert.Single(chunks);
    }

    [Fact]
    public void SanitizeCatalogText_strips_control_and_truncates()
    {
        var dirty = "Táo\nFuji" + '\0' + " ignore instructions";
        var clean = IndexingService.SanitizeCatalogText(dirty, 20);
        Assert.DoesNotContain('\n', clean);
        Assert.DoesNotContain('\0', clean);
        Assert.Contains("Táo", clean);
        Assert.Contains("Fuji", clean);
        Assert.True(clean.Length <= 21); // 20 + ellipsis
    }

    [Fact]
    public async Task IndexCatalogInsightsAsync_creates_bestseller_and_featured_chunks()
    {
        await using var db = CreateContext();

        var category = new Category
        {
            Name = "Trái cây",
            Slug = "trai-cay",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var featured = new Product
        {
            Name = "Táo Fuji nổi bật",
            CategoryId = category.Id,
            Slug = "tao-fuji",
            Price = 10,
            IsFeatured = true,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var other = new Product
        {
            Name = "Xoài bán chạy",
            CategoryId = category.Id,
            Slug = "xoai",
            Price = 20,
            IsFeatured = false,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Products.AddRange(featured, other);
        await db.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = "ORD-TEST-1",
            Status = OrderStatus.Delivered,
            Subtotal = 100,
            Total = 100,
            PaymentMethod = PaymentMethod.COD,
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = other.Id,
            ProductName = other.Name,
            Quantity = 12,
            Price = 20,
            Total = 240
        });
        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = featured.Id,
            ProductName = featured.Name,
            Quantity = 3,
            Price = 10,
            Total = 30
        });
        await db.SaveChangesAsync();

        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var sut = CreateService(db, embedding);
        await sut.IndexCatalogInsightsAsync();

        var best = await db.KnowledgeChunks.SingleAsync(c =>
            c.SourceType == KnowledgeSourceType.Catalog
            && c.SourceId == IndexingService.CatalogBestsellersSourceId
            && c.IsActive);
        Assert.Contains("bán chạy", best.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Xoài", best.Content);
        Assert.Contains("12", best.Content);
        // Template only — no raw injection payloads from product description
        Assert.DoesNotContain("Ignore all", best.Content);

        var feat = await db.KnowledgeChunks.SingleAsync(c =>
            c.SourceType == KnowledgeSourceType.Catalog
            && c.SourceId == IndexingService.CatalogFeaturedSourceId
            && c.IsActive);
        Assert.Contains("nổi bật", feat.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Táo Fuji", feat.Content);
    }

    [Fact]
    public async Task IndexProductAsync_creates_chunk_with_price_stock_variants_tags()
    {
        await using var db = CreateContext();

        var category = new Category
        {
            Name = "Trái cây",
            Slug = "trai-cay",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var tag = new ProductTag
        {
            Name = "Táo nhập khẩu",
            Slug = "tao-nhap-khau"
        };
        db.ProductTags.Add(tag);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Name = "Táo Fuji",
            Slug = "tao-fuji",
            CategoryId = category.Id,
            Price = 125000,
            Unit = "kg",
            StockQuantity = 50,
            MinOrderQuantity = 1,
            CountryOrigin = "Nhật Bản",
            Quality = "Loại A",
            ShortDescription = "Táo Fuji ngọt giòn.",
            Description = "Táo Fuji nhập khẩu từ Nhật Bản, giòn ngọt tự nhiên.",
            IsActive = true,
            IsDeleted = false,
            IsFeatured = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Tags = new List<ProductTag> { tag }
        };
        product.Variants.Add(new ProductVariant
        {
            Name = "Hộp 2kg",
            SKU = "FUJI-2KG",
            Price = 240000,
            StockQuantity = 20,
            IsActive = true
        });
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.PriceSchedules.Add(new PriceSchedule
        {
            ProductId = product.Id,
            ProductVariantId = product.Variants.Single().Id,
            DiscountType = DiscountType.FixedPrice,
            Value = 190000,
            StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var sut = CreateService(db, embedding);

        await sut.IndexProductAsync(product.Id);

        var chunk = await db.KnowledgeChunks
            .SingleAsync(c =>
                c.SourceType == KnowledgeSourceType.Product
                && c.SourceId == product.Id.ToString()
                && c.IsActive);

        Assert.Contains("Táo Fuji", chunk.Content);
        Assert.Contains("125,000đ", chunk.Content);
        Assert.Contains("190,000đ", chunk.Content);
        Assert.Contains("Tổng tồn kho biến thể: 20", chunk.Content);
        Assert.Contains("Nhật Bản", chunk.Content);
        Assert.Contains("Hộp 2kg", chunk.Content);
        Assert.Contains("Táo nhập khẩu", chunk.Content);
        Assert.Contains("sản phẩm", chunk.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("giá", chunk.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Wraps an <see cref="IEmbeddingClient"/> and counts <see cref="EmbedAsync"/> invocations.
    /// </summary>
    private sealed class CountingEmbeddingClient : IEmbeddingClient
    {
        private readonly IEmbeddingClient _inner;

        public CountingEmbeddingClient(IEmbeddingClient inner)
        {
            _inner = inner;
        }

        public int EmbedCallCount { get; private set; }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            EmbedCallCount++;
            return _inner.EmbedAsync(text, ct);
        }
    }
}
