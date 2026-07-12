using System.Security.Cryptography;
using System.Text;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat;
using Fruitables.Services.Interfaces;
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
        var faq = new Faq
        {
            Title = "Phí ship",
            Body = "Nội thành 30k",
            Category = "shipping",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Faqs.Add(faq);
        await db.SaveChangesAsync();

        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var sut = CreateService(db, embedding);

        await sut.IndexFaqAsync(faq.Id);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == faq.Id.ToString())
            .ToListAsync();

        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.True(chunk.IsActive);
        Assert.Equal("Phí ship", chunk.Title);
        Assert.Equal("Phí ship\n\nNội thành 30k", chunk.Content);
        Assert.False(string.IsNullOrWhiteSpace(chunk.ContentHash));
        Assert.False(string.IsNullOrWhiteSpace(chunk.EmbeddingJson));
        Assert.NotEqual("[]", chunk.EmbeddingJson);
        Assert.Equal(Sha256Hex(chunk.Content), chunk.ContentHash);
    }

    [Fact]
    public async Task IndexFaqAsync_inactive_disables_chunks()
    {
        await using var db = CreateContext();
        var faq = new Faq
        {
            Title = "Đổi trả",
            Body = "Trong 7 ngày",
            Category = "policy",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Faqs.Add(faq);
        await db.SaveChangesAsync();

        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var sut = CreateService(db, embedding);

        await sut.IndexFaqAsync(faq.Id);
        Assert.True(await db.KnowledgeChunks.AnyAsync(c => c.IsActive));

        faq.IsActive = false;
        await db.SaveChangesAsync();

        await sut.IndexFaqAsync(faq.Id);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == faq.Id.ToString())
            .ToListAsync();

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.False(c.IsActive));
    }

    [Fact]
    public async Task IndexFaqAsync_same_content_skips_reembed()
    {
        await using var db = CreateContext();
        var faq = new Faq
        {
            Title = "Giờ mở cửa",
            Body = "8h-20h mỗi ngày",
            Category = "general",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Faqs.Add(faq);
        await db.SaveChangesAsync();

        var inner = new DeterministicEmbeddingClient(dimensions: 32);
        var counting = new CountingEmbeddingClient(inner);
        var sut = CreateService(db, counting);

        await sut.IndexFaqAsync(faq.Id);
        Assert.Equal(1, counting.EmbedCallCount);

        await sut.IndexFaqAsync(faq.Id);
        Assert.Equal(1, counting.EmbedCallCount);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == faq.Id.ToString() && c.IsActive)
            .ToListAsync();
        Assert.Single(chunks);
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
