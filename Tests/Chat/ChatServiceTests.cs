using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat;
using Fruitables.Services.Chat.Intents;
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
    private static IIntentRouter CreateGeneralIntentRouter()
    {
        var router = new Mock<IIntentRouter>();
        router.Setup(service => service.ClassifyAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatIntent.Of(ChatIntentKind.GeneralInquiry, 0.5f));
        return router.Object;
    }

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
            CreateGeneralIntentRouter(),
            Mock.Of<IProductService>(),
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

    [Fact]
    public async Task SendStreamingAsync_yields_meta_tokens_done_and_persists()
    {
        await using var db = CreateContext();
        var rag = new Mock<IRagService>();
        rag.Setup(r => r.AnswerStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(StreamParts(
                RagStreamPart.Token("Xin "),
                RagStreamPart.Token("chào"),
                RagStreamPart.Complete("Xin chào", new List<long> { 9 })));

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ChatService(
            db,
            rag.Object,
            CreateGeneralIntentRouter(),
            Mock.Of<IProductService>(),
            cache,
            Microsoft.Extensions.Options.Options.Create(new ChatOptions()),
            NullLogger<ChatService>.Instance);

        var sessionId = await sut.CreateSessionAsync(null, "widget");
        var events = new List<ChatStreamEvent>();
        await foreach (var evt in sut.SendStreamingAsync(sessionId, "Hello", null, "127.0.0.1"))
            events.Add(evt);

        Assert.Equal("meta", events[0].Type);
        Assert.Equal(sessionId, events[0].SessionId);
        Assert.Contains(events, e => e.Type == "token" && e.Text == "Xin ");
        Assert.Contains(events, e => e.Type == "token" && e.Text == "chào");
        var done = Assert.Single(events, e => e.Type == "done");
        Assert.Equal("Xin chào", done.Text);
        Assert.False(done.Refused);
        Assert.True(done.MessageId > 0);

        var messages = await db.ChatMessages.Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("Hello", messages[0].Content);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("Xin chào", messages[1].Content);
        Assert.Contains("streamed", messages[1].MetaJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendStreamingAsync_refuse_persists_refused_meta()
    {
        await using var db = CreateContext();
        var rag = new Mock<IRagService>();
        rag.Setup(r => r.AnswerStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(StreamParts(RagStreamPart.Refuse("Chưa có thông tin.")));

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ChatService(
            db,
            rag.Object,
            CreateGeneralIntentRouter(),
            Mock.Of<IProductService>(),
            cache,
            Microsoft.Extensions.Options.Options.Create(new ChatOptions()),
            NullLogger<ChatService>.Instance);

        var sessionId = await sut.CreateSessionAsync(null, "page");
        var events = new List<ChatStreamEvent>();
        await foreach (var evt in sut.SendStreamingAsync(sessionId, "??? ", null, "10.0.0.1"))
            events.Add(evt);

        Assert.Contains(events, e => e.Type == "token" && e.Text == "Chưa có thông tin.");
        var done = Assert.Single(events, e => e.Type == "done");
        Assert.True(done.Refused);
        Assert.Equal("Chưa có thông tin.", done.Text);

        var assistant = await db.ChatMessages.SingleAsync(m => m.SessionId == sessionId && m.Role == "assistant");
        Assert.Contains("\"refused\":true", assistant.MetaJson!.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
    }

    private static async IAsyncEnumerable<RagStreamPart> StreamParts(params RagStreamPart[] parts)
    {
        foreach (var part in parts)
        {
            yield return part;
            await Task.Yield();
        }
    }
}
