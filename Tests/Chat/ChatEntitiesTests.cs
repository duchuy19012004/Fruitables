using Fruitables.Data;
using Fruitables.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests.Chat;

public class ChatEntitiesTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Can_persist_faq_session_message_and_chunk()
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

        var sessionId = Guid.NewGuid();
        var session = new ChatSession
        {
            Id = sessionId,
            UserId = null, // guest session
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            Source = "page"
        };
        db.ChatSessions.Add(session);

        db.ChatMessages.Add(new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = "Ship bao nhiêu?",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // KnowledgeChunk.SourceId links to the FAQ after it has a real Id
        var chunk = new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.Faq,
            SourceId = faq.Id.ToString(),
            Title = "Phí ship",
            Content = "Nội thành 30k",
            EmbeddingJson = "[0.1,0.2]",
            ContentHash = "abc",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };
        db.KnowledgeChunks.Add(chunk);
        await db.SaveChangesAsync();

        // Round-trip assertions
        var savedFaq = await db.Faqs.SingleAsync();
        Assert.Equal("Phí ship", savedFaq.Title);
        Assert.Equal("Nội thành 30k", savedFaq.Body);

        var savedSession = await db.ChatSessions.SingleAsync();
        Assert.Null(savedSession.UserId);
        Assert.Equal(sessionId, savedSession.Id);

        var savedMessage = await db.ChatMessages.SingleAsync();
        Assert.Equal("user", savedMessage.Role);
        Assert.Equal("Ship bao nhiêu?", savedMessage.Content);
        Assert.Equal(sessionId, savedMessage.SessionId);

        var savedChunk = await db.KnowledgeChunks.SingleAsync();
        Assert.Equal(KnowledgeSourceType.Faq, savedChunk.SourceType);
        Assert.Equal(faq.Id.ToString(), savedChunk.SourceId);
        Assert.Equal("Nội thành 30k", savedChunk.Content);
        Assert.Equal("[0.1,0.2]", savedChunk.EmbeddingJson);
        Assert.Equal("abc", savedChunk.ContentHash);
    }
}
