using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Tests.Chat.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fruitables.Tests.Chat;

public class FaqServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static FaqService CreateService(ApplicationDbContext db, DeterministicEmbeddingClient? embedding = null)
    {
        embedding ??= new DeterministicEmbeddingClient(dimensions: 32);
        var indexing = new IndexingService(
            db,
            embedding,
            Microsoft.Extensions.Options.Options.Create(new ChatOptions()),
            NullLogger<IndexingService>.Instance);

        return new FaqService(db, indexing, NullLogger<FaqService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_indexes_faq()
    {
        await using var db = CreateContext();
        var sut = CreateService(db);

        var faq = await sut.CreateAsync(
            title: "Phí ship",
            body: "Nội thành 30k",
            category: "shipping",
            isActive: true);

        Assert.True(faq.Id > 0);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == faq.Id.ToString())
            .ToListAsync();

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.True(c.IsActive));
        Assert.Contains(chunks, c => c.Content.Contains("Phí ship") || c.Content.Contains("Nội thành"));
    }

    [Fact]
    public async Task UpdateAsync_reindexes()
    {
        await using var db = CreateContext();
        var sut = CreateService(db);

        var faq = await sut.CreateAsync("Cũ", "Nội dung cũ", "general", isActive: true);

        var updated = await sut.UpdateAsync(
            faq.Id,
            title: "Mới",
            body: "Nội dung đã cập nhật hoàn toàn",
            category: "policy",
            isActive: true);

        Assert.NotNull(updated);
        Assert.Equal("Mới", updated!.Title);
        Assert.Equal("policy", updated.Category);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq
                        && c.SourceId == faq.Id.ToString()
                        && c.IsActive)
            .ToListAsync();

        Assert.NotEmpty(chunks);
        Assert.Contains(chunks, c => c.Content.Contains("Mới") || c.Content.Contains("cập nhật"));
    }

    [Fact]
    public async Task SetActive_false_disables_chunks()
    {
        await using var db = CreateContext();
        var sut = CreateService(db);

        var faq = await sut.CreateAsync("Hỗ trợ đơn hàng", "Liên hệ cửa hàng", "support", isActive: true);

        Assert.True(await db.KnowledgeChunks.AnyAsync(c =>
            c.SourceType == KnowledgeSourceType.Faq
            && c.SourceId == faq.Id.ToString()
            && c.IsActive));

        await sut.SetActiveAsync(faq.Id, isActive: false);

        var dbFaq = await db.ContentEntries.AsNoTracking()
            .SingleAsync(entry => entry.Id == faq.Id && entry.EntryType == "faq");
        Assert.False(dbFaq.IsActive);

        var chunks = await db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Faq && c.SourceId == faq.Id.ToString())
            .ToListAsync();

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.False(c.IsActive));
    }
}
