using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat;
using Fruitables.Tests.Chat.Fakes;
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
        Assert.Contains("liên hệ", answer.Content, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("liên hệ", answer.Content, StringComparison.OrdinalIgnoreCase);
    }
}
