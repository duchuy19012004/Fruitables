using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fruitables.Tests.Chat;

public class ChatServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (ChatService Sut, Mock<IRagService> Rag, IMemoryCache Cache) CreateSut(
        ApplicationDbContext db,
        ChatOptions? chatOptions = null,
        RagAnswer? answer = null)
    {
        var rag = new Mock<IRagService>();
        rag.Setup(r => r.AnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer ?? new RagAnswer
            {
                Content = "Xin chào, phí ship nội thành là 30.000đ.",
                Refused = false,
                SourceChunkIds = new List<long> { 1 }
            });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ChatService(
            db,
            rag.Object,
            cache,
            Microsoft.Extensions.Options.Options.Create(chatOptions ?? new ChatOptions()),
            NullLogger<ChatService>.Instance);

        return (sut, rag, cache);
    }

    [Fact]
    public async Task SendAsync_persists_user_and_assistant()
    {
        await using var db = CreateContext();
        var (sut, rag, _) = CreateSut(db);

        var sessionId = await sut.CreateSessionAsync(userId: null, source: "widget");
        var response = await sut.SendAsync(sessionId, "  Phí ship bao nhiêu?  ", userId: null, clientIp: "127.0.0.1");

        Assert.Equal(sessionId, response.SessionId);
        Assert.Equal("assistant", response.AssistantMessage.Role);
        Assert.Equal("Xin chào, phí ship nội thành là 30.000đ.", response.AssistantMessage.Content);
        Assert.False(response.AssistantMessage.Refused);

        var messages = await db.ChatMessages.Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("Phí ship bao nhiêu?", messages[0].Content);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Contains("refused", messages[1].MetaJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chunkIds", messages[1].MetaJson, StringComparison.OrdinalIgnoreCase);

        var session = await db.ChatSessions.SingleAsync(s => s.Id == sessionId);
        Assert.Equal(messages[1].CreatedAt, session.LastMessageAt);

        rag.Verify(r => r.AnswerAsync("Phí ship bao nhiêu?", It.IsAny<CancellationToken>()), Times.Once);

        var history = await sut.GetMessagesAsync(sessionId);
        Assert.Equal(2, history.Count);
        Assert.False(history[1].Refused);
    }

    [Fact]
    public async Task SendAsync_rejects_empty_or_too_long()
    {
        await using var db = CreateContext();
        var options = new ChatOptions { MaxUserMessageChars = 10 };
        var (sut, rag, _) = CreateSut(db, options);
        var sessionId = await sut.CreateSessionAsync(null, "page");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SendAsync(sessionId, "   ", null, "1.1.1.1"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SendAsync(sessionId, "this is way too long", null, "1.1.1.1"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SendAsync(sessionId, string.Empty, null, "1.1.1.1"));

        rag.Verify(r => r.AnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(await db.ChatMessages.ToListAsync());
    }

    [Fact]
    public async Task SendAsync_rate_limits()
    {
        await using var db = CreateContext();
        var options = new ChatOptions { RateLimitPerMinute = 2 };
        var (sut, _, _) = CreateSut(db, options);
        var sessionId = await sut.CreateSessionAsync(null, "widget");
        const string ip = "10.0.0.5";

        await sut.SendAsync(sessionId, "msg 1", null, ip);
        await sut.SendAsync(sessionId, "msg 2", null, ip);

        await Assert.ThrowsAsync<ChatRateLimitException>(() =>
            sut.SendAsync(sessionId, "msg 3", null, ip));

        // Same IP is still rate-limited across sessions (key is IP-scoped, not session-scoped)
        var otherSessionId = await sut.CreateSessionAsync(null, "widget");
        await Assert.ThrowsAsync<ChatRateLimitException>(() =>
            sut.SendAsync(otherSessionId, "msg via other session", null, ip));

        // Only two user + two assistant messages persisted (third/fourth blocked before save)
        Assert.Equal(4, await db.ChatMessages.CountAsync());
    }
}
