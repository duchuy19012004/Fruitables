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

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            Source = "page"
        };
        db.ChatSessions.Add(session);

        db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = "Ship bao nhiêu?",
            CreatedAt = DateTime.UtcNow
        });

        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.Faq,
            SourceId = "0",
            Title = "Phí ship",
            Content = "Nội thành 30k",
            EmbeddingJson = "[0.1,0.2]",
            ContentHash = "abc",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Faqs.CountAsync());
        Assert.Equal(1, await db.ChatSessions.CountAsync());
        Assert.Equal(1, await db.ChatMessages.CountAsync());
        Assert.Equal(1, await db.KnowledgeChunks.CountAsync());
    }
}
