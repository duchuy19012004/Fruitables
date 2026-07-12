using System.Security.Claims;
using Fruitables.Controllers.Api;
using Fruitables.Services.Chat;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fruitables.Tests.Chat;

public class ChatApiControllerTests
{
    private static ChatApiController CreateController(
        Mock<IChatService> chatService,
        string? cookieSessionId = null,
        int? authenticatedUserId = null,
        bool isHttps = false)
    {
        var controller = new ChatApiController(
            chatService.Object,
            NullLogger<ChatApiController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.IsHttps = isHttps;
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");

        if (cookieSessionId is not null)
        {
            httpContext.Request.Headers.Cookie =
                $"{ChatApiController.SessionCookieName}={cookieSessionId}";
        }

        if (authenticatedUserId is not null)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString())
            }, authenticationType: "Test");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task CreateSession_sets_cookie_and_returns_sessionId()
    {
        var sessionId = Guid.NewGuid();
        var chat = new Mock<IChatService>();
        chat.Setup(s => s.CreateSessionAsync(null, "widget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionId);

        var controller = CreateController(chat);
        var result = await controller.CreateSession(
            new CreateChatSessionRequest { Source = "widget" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        var setCookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains(ChatApiController.SessionCookieName, setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(sessionId.ToString(), setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);

        chat.Verify(s => s.CreateSessionAsync(null, "widget", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSession_passes_authenticated_userId()
    {
        var sessionId = Guid.NewGuid();
        var chat = new Mock<IChatService>();
        chat.Setup(s => s.CreateSessionAsync(42, "page", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionId);

        var controller = CreateController(chat, authenticatedUserId: 42);
        var result = await controller.CreateSession(
            new CreateChatSessionRequest { Source = "page" },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        chat.Verify(s => s.CreateSessionAsync(42, "page", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_uses_cookie_session_and_returns_response()
    {
        var sessionId = Guid.NewGuid();
        var chat = new Mock<IChatService>();
        chat.Setup(s => s.SendAsync(
                sessionId,
                "Phí ship?",
                null,
                "203.0.113.10",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendChatMessageResponse
            {
                SessionId = sessionId,
                AssistantMessage = new ChatMessageDto
                {
                    Id = 1,
                    Role = "assistant",
                    Content = "30.000đ",
                    CreatedAt = DateTime.UtcNow,
                    Refused = false
                }
            });

        var controller = CreateController(chat, cookieSessionId: sessionId.ToString());
        var result = await controller.SendMessage(
            new SendChatMessageRequest { Message = "Phí ship?", Source = "widget" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SendChatMessageResponse>(ok.Value);
        Assert.Equal(sessionId, body.SessionId);
        Assert.Equal("30.000đ", body.AssistantMessage.Content);

        chat.Verify(s => s.CreateSessionAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_creates_session_when_no_cookie_or_body()
    {
        var sessionId = Guid.NewGuid();
        var chat = new Mock<IChatService>();
        chat.Setup(s => s.CreateSessionAsync(null, "widget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionId);
        chat.Setup(s => s.SendAsync(sessionId, "hello", null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendChatMessageResponse
            {
                SessionId = sessionId,
                AssistantMessage = new ChatMessageDto
                {
                    Role = "assistant",
                    Content = "hi",
                    CreatedAt = DateTime.UtcNow
                }
            });

        var controller = CreateController(chat);
        var result = await controller.SendMessage(
            new SendChatMessageRequest { Message = "hello", Source = "widget" },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var setCookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains(sessionId.ToString(), setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_ArgumentException_returns_400()
    {
        var sessionId = Guid.NewGuid();
        var chat = new Mock<IChatService>();
        chat.Setup(s => s.SendAsync(sessionId, It.IsAny<string>(), null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Message cannot be empty."));

        var controller = CreateController(chat, cookieSessionId: sessionId.ToString());
        var result = await controller.SendMessage(
            new SendChatMessageRequest { Message = "   " },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task SendMessage_rate_limit_returns_429()
    {
        var sessionId = Guid.NewGuid();
        var chat = new Mock<IChatService>();
        chat.Setup(s => s.SendAsync(sessionId, It.IsAny<string>(), null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChatRateLimitException("Rate limit exceeded."));

        var controller = CreateController(chat, cookieSessionId: sessionId.ToString());
        var result = await controller.SendMessage(
            new SendChatMessageRequest { Message = "again" },
            CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(429, status.StatusCode);
    }

    [Fact]
    public async Task SendMessage_missing_session_retries_with_new_session()
    {
        var staleId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var chat = new Mock<IChatService>();

        chat.Setup(s => s.SendAsync(staleId, "hi", null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException($"Chat session '{staleId}' was not found."));
        chat.Setup(s => s.CreateSessionAsync(null, "widget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newId);
        chat.Setup(s => s.SendAsync(newId, "hi", null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendChatMessageResponse
            {
                SessionId = newId,
                AssistantMessage = new ChatMessageDto
                {
                    Role = "assistant",
                    Content = "ok",
                    CreatedAt = DateTime.UtcNow
                }
            });

        var controller = CreateController(chat, cookieSessionId: staleId.ToString());
        var result = await controller.SendMessage(
            new SendChatMessageRequest { Message = "hi", Source = "widget" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SendChatMessageResponse>(ok.Value);
        Assert.Equal(newId, body.SessionId);

        var setCookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains(newId.ToString(), setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMessages_forbidden_when_cookie_mismatch()
    {
        var chat = new Mock<IChatService>();
        var controller = CreateController(chat, cookieSessionId: Guid.NewGuid().ToString());

        var result = await controller.GetMessages(Guid.NewGuid(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
        chat.Verify(s => s.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMessages_returns_history_when_cookie_matches()
    {
        var sessionId = Guid.NewGuid();
        var chat = new Mock<IChatService>();
        chat.Setup(s => s.GetMessagesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageDto>
            {
                new() { Id = 1, Role = "user", Content = "hi", CreatedAt = DateTime.UtcNow },
                new() { Id = 2, Role = "assistant", Content = "hello", CreatedAt = DateTime.UtcNow }
            });

        var controller = CreateController(chat, cookieSessionId: sessionId.ToString());
        var result = await controller.GetMessages(sessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var messages = Assert.IsAssignableFrom<IReadOnlyList<ChatMessageDto>>(ok.Value);
        Assert.Equal(2, messages.Count);
    }
}
