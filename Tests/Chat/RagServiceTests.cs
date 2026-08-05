using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Chat.Providers;
using Fruitables.Tests.Chat.Fakes;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fruitables.Tests.Chat;

public class RagServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static RagService CreateService(
        ApplicationDbContext db,
        DeterministicEmbeddingClient embedding,
        FakeLlmClient llm,
        ChatOptions? chatOptions = null)
    {
        return new RagService(
            db,
            embedding,
            llm,
            Microsoft.Extensions.Options.Options.Create(chatOptions ?? new ChatOptions()),
            NullLogger<RagService>.Instance);
    }

    [Fact]
    public async Task AnswerAsync_high_score_calls_llm_with_context()
    {
        await using var db = CreateContext();
        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var llm = new FakeLlmClient { Response = "Phí ship nội thành là 30.000đ." };

        const string kbText = "phí ship nội thành 30000";
        var vector = await embedding.EmbedAsync(kbText);
        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.Faq,
            SourceId = "1",
            Title = "Phí ship",
            Content = kbText,
            EmbeddingJson = EmbeddingSerializer.ToJson(vector),
            ContentHash = "hash-ship",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db, embedding, llm);

        var answer = await sut.AnswerAsync("phí ship nội thành 30000");

        Assert.False(answer.Refused);
        Assert.Equal(llm.Response, answer.Content);
        Assert.Single(llm.Calls);
        Assert.Contains("CONTEXT", llm.Calls[0].User, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(answer.SourceChunkIds);
    }

    [Fact]
    public async Task AnswerAsync_low_score_refuses_without_llm()
    {
        await using var db = CreateContext();
        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var llm = new FakeLlmClient { Response = "should not be used" };

        // Empty knowledge base → no retrieval → refuse without LLM.
        var sut = CreateService(db, embedding, llm);

        var answer = await sut.AnswerAsync("phí ship nội thành bao nhiêu?");

        Assert.True(answer.Refused);
        Assert.Empty(llm.Calls);
        Assert.Empty(answer.SourceChunkIds);
        Assert.Equal("Mình chưa tìm thấy thông tin phù hợp để trả lời câu này.", answer.Content);
    }

    [Fact]
    public async Task AnswerAsync_unrelated_chunk_below_threshold_refuses()
    {
        await using var db = CreateContext();
        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var llm = new FakeLlmClient();

        const string kbText = "cách nấu canh bí đỏ với tôm";
        var vector = await embedding.EmbedAsync(kbText);
        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.Faq,
            SourceId = "99",
            Title = "Nấu ăn",
            Content = kbText,
            EmbeddingJson = EmbeddingSerializer.ToJson(vector),
            ContentHash = "hash-unrelated",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // High threshold so only strong matches pass.
        var sut = CreateService(db, embedding, llm, new ChatOptions { MinScore = 0.95f, TopK = 5 });

        var answer = await sut.AnswerAsync("phí ship nội thành 30000");

        Assert.True(answer.Refused);
        Assert.Empty(llm.Calls);
        Assert.Equal("Mình chưa tìm thấy thông tin phù hợp để trả lời câu này.", answer.Content);
    }

    [Fact]
    public async Task AnswerAsync_lexical_match_answers_phi_ship_without_identical_embedding()
    {
        // LocalHash: query "Phí ship?" ≠ exact FAQ text, but hybrid lexical + synonym should hit.
        await using var db = CreateContext();
        var embedding = new LocalHashEmbeddingClient(
            Microsoft.Extensions.Options.Options.Create(new ChatOptions { EmbeddingDimensions = 256 }));
        var llm = new FakeLlmClient { Response = "Phí ship tính theo khu vực khi checkout." };

        const string faq =
            "Phí vận chuyển như thế nào?\n\n" +
            "Phí vận chuyển được tính theo khu vực: nội thành, tỉnh lân cận, tỉnh xa.\n\n" +
            "Từ khóa: phí ship phí vận chuyển giao hàng shipping free ship COD";
        var vector = await embedding.EmbedAsync(faq);
        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.Faq,
            SourceId = "1",
            Title = "Phí vận chuyển như thế nào?",
            Content = faq,
            EmbeddingJson = EmbeddingSerializer.ToJson(vector),
            ContentHash = "hash-ship-faq",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new RagService(
            db,
            embedding,
            llm,
            Microsoft.Extensions.Options.Options.Create(new ChatOptions { MinScore = 0.32f, TopK = 5 }),
            NullLogger<RagService>.Instance);

        var answer = await sut.AnswerAsync("Phí ship?");

        Assert.False(answer.Refused);
        Assert.Equal(llm.Response, answer.Content);
        Assert.Single(llm.Calls);
    }

    [Fact]
    public async Task AnswerAsync_product_price_question_matches_product_chunk()
    {
        await using var db = CreateContext();
        var embedding = new LocalHashEmbeddingClient(
            Microsoft.Extensions.Options.Options.Create(new ChatOptions { EmbeddingDimensions = 256 }));
        var llm = new FakeLlmClient { Response = "Táo Fuji giá 125.000đ/kg, đang khuyến mãi còn 99.000đ/kg." };

        var product = new Product
        {
            Name = "Táo Fuji",
            Slug = "tao-fuji",
            Category = new Category { Name = "Trái cây", Slug = "trai-cay" },
            Price = 125000,
            SalePrice = 99000,
            Unit = "kg",
            StockQuantity = 50,
            ShortDescription = "Táo Fuji ngọt giòn.",
            IsActive = true,
            IsFeatured = false
        };
        var text = IndexingService.BuildProductText(product);
        var vector = await embedding.EmbedAsync(text);
        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.Product,
            SourceId = "42",
            Title = product.Name,
            Content = text,
            EmbeddingJson = EmbeddingSerializer.ToJson(vector),
            ContentHash = "hash-product",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new RagService(
            db,
            embedding,
            llm,
            Microsoft.Extensions.Options.Options.Create(new ChatOptions { MinScore = 0.32f, TopK = 5 }),
            NullLogger<RagService>.Instance);

        var answer = await sut.AnswerAsync("Táo Fuji giá bao nhiêu?");

        Assert.False(answer.Refused);
        Assert.Single(llm.Calls);
        Assert.Contains("Táo Fuji", llm.Calls[0].User);
        Assert.Contains("125,000đ", llm.Calls[0].User);
    }

    [Fact]
    public async Task AnswerStreamingAsync_high_score_yields_tokens_then_complete()
    {
        await using var db = CreateContext();
        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var llm = new FakeLlmClient
        {
            Response = "Phí ship 30k",
            StreamChunkSize = 4
        };

        const string kbText = "phí ship nội thành 30000";
        var vector = await embedding.EmbedAsync(kbText);
        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.Faq,
            SourceId = "1",
            Title = "Phí ship",
            Content = kbText,
            EmbeddingJson = EmbeddingSerializer.ToJson(vector),
            ContentHash = "hash-ship-stream",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db, embedding, llm);
        var parts = new List<RagStreamPart>();
        await foreach (var part in sut.AnswerStreamingAsync("phí ship nội thành 30000"))
            parts.Add(part);

        Assert.Contains(parts, p => p.Kind == "token");
        var complete = Assert.Single(parts, p => p.Kind == "complete");
        Assert.Equal("Phí ship 30k", complete.Text);
        Assert.False(complete.Refused);
        Assert.Equal("Phí ship 30k", string.Concat(parts.Where(p => p.Kind == "token").Select(p => p.Text)));
        Assert.Single(llm.Calls);
    }

    [Fact]
    public async Task AnswerStreamingAsync_low_score_yields_refuse_only()
    {
        await using var db = CreateContext();
        var embedding = new DeterministicEmbeddingClient(dimensions: 32);
        var llm = new FakeLlmClient { Response = "should not stream" };

        var sut = CreateService(db, embedding, llm);
        var parts = new List<RagStreamPart>();
        await foreach (var part in sut.AnswerStreamingAsync("câu hỏi không liên quan xyz"))
            parts.Add(part);

        var refused = Assert.Single(parts);
        Assert.Equal("refused", refused.Kind);
        Assert.True(refused.Refused);
        Assert.Empty(llm.Calls);
    }
}
