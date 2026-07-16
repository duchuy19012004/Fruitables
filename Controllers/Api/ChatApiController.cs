using System.Security.Claims;
using System.Text.Json;
using Fruitables.Services.Chat;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Controllers.Api;

[ApiController]
[Route("api/chat")]
public class ChatApiController : ControllerBase
{
    public const string SessionCookieName = "chat_session_id";

    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IChatService _chatService;
    private readonly ILogger<ChatApiController> _logger;

    public ChatApiController(IChatService chatService, ILogger<ChatApiController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>POST api/chat/sessions — create a chat session and set session cookie.</summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateChatSessionRequest? request,
        CancellationToken ct)
    {
        try
        {
            var userId = GetOptionalUserId();
            var sessionId = await _chatService.CreateSessionAsync(userId, request?.Source, ct);
            SetSessionCookie(sessionId);
            return Ok(new { sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat session");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>POST api/chat/messages — send a user message and return the assistant reply.</summary>
    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendChatMessageRequest request,
        CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });

        try
        {
            var userId = GetOptionalUserId();
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var sessionId = await ResolveOrCreateSessionAsync(request.SessionId, request.Source, userId, ct);

            try
            {
                var response = await _chatService.SendAsync(
                    sessionId, request.Message, userId, clientIp, ct);
                return Ok(response);
            }
            catch (ChatSessionNotFoundException)
            {
                // Missing/stale session: create a new one and retry once
                sessionId = await _chatService.CreateSessionAsync(userId, request.Source, ct);
                SetSessionCookie(sessionId);
                var response = await _chatService.SendAsync(
                    sessionId, request.Message, userId, clientIp, ct);
                return Ok(response);
            }
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ChatRateLimitException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send chat message");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>GET api/chat/sessions/{id}/messages — history only if cookie matches session id.</summary>
    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, CancellationToken ct)
    {
        if (!TryGetSessionIdFromCookie(out var cookieSessionId) || cookieSessionId != id)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden" });

        try
        {
            var messages = await _chatService.GetMessagesAsync(id, ct);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat messages for session {SessionId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau."
            });
        }
    }

    /// <summary>
    /// POST api/chat/messages/stream — SSE stream (meta / token / done / error) for storefront chat.js.
    /// Early validation failures return JSON status codes; mid-stream failures emit an error event.
    /// </summary>
    [HttpPost("messages/stream")]
    public async Task SendMessageStream(
        [FromBody] SendChatMessageRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "Request body is required." }, cancellationToken: ct);
            return;
        }

        var userId = GetOptionalUserId();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var sessionId = await ResolveOrCreateSessionAsync(request.SessionId, request.Source, userId, ct);
            await StreamWithSessionRetryAsync(sessionId, request.Message, userId, clientIp, request.Source, ct);
        }
        catch (ArgumentException ex)
        {
            await WriteEarlyOrStreamErrorAsync(StatusCodes.Status400BadRequest, ex.Message, ct);
        }
        catch (ChatRateLimitException ex)
        {
            await WriteEarlyOrStreamErrorAsync(StatusCodes.Status429TooManyRequests, ex.Message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream chat message");
            await WriteEarlyOrStreamErrorAsync(
                StatusCodes.Status503ServiceUnavailable,
                "Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau.",
                ct);
        }
    }

    private async Task StreamWithSessionRetryAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        string? source,
        CancellationToken ct)
    {
        try
        {
            await WriteSseStreamAsync(sessionId, message, userId, clientIp, ct);
        }
        catch (ChatSessionNotFoundException)
        {
            // Missing/stale session: create a new one and retry once (same as non-stream path)
            sessionId = await _chatService.CreateSessionAsync(userId, source, ct);
            SetSessionCookie(sessionId);
            await WriteSseStreamAsync(sessionId, message, userId, clientIp, ct);
        }
    }

    private async Task WriteSseStreamAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        CancellationToken ct)
    {
        // Peek the first event so ArgumentException / rate-limit / session-not-found
        // surface before we commit the response to text/event-stream.
        await using var enumerator = _chatService
            .SendStreamingAsync(sessionId, message, userId, clientIp, ct)
            .GetAsyncEnumerator(ct);

        if (!await enumerator.MoveNextAsync())
            throw new InvalidOperationException("Chat stream produced no events.");

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        do
        {
            await WriteChatStreamEventAsync(enumerator.Current, ct);
        }
        while (await enumerator.MoveNextAsync());
    }

    private async Task WriteChatStreamEventAsync(ChatStreamEvent evt, CancellationToken ct)
    {
        object payload = evt.Type switch
        {
            "meta" => new { sessionId = evt.SessionId },
            "token" => new { text = evt.Text },
            "done" => new
            {
                sessionId = evt.SessionId,
                text = evt.Text,
                refused = evt.Refused ?? false,
                messageId = evt.MessageId
            },
            "error" => new { error = evt.Error },
            _ => new { }
        };

        await WriteSseEventAsync(evt.Type, payload, ct);
    }

    private async Task WriteSseEventAsync(string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SseJsonOptions);
        await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task WriteEarlyOrStreamErrorAsync(int statusCode, string error, CancellationToken ct)
    {
        if (Response.HasStarted)
        {
            await WriteSseEventAsync("error", new { error }, ct);
            return;
        }

        Response.StatusCode = statusCode;
        await Response.WriteAsJsonAsync(new { error }, cancellationToken: ct);
    }

    private async Task<Guid> ResolveOrCreateSessionAsync(
        Guid? bodySessionId,
        string? source,
        int? userId,
        CancellationToken ct)
    {
        if (TryGetSessionIdFromCookie(out var cookieSessionId))
            return cookieSessionId;

        if (bodySessionId is { } id && id != Guid.Empty)
            return id;

        var sessionId = await _chatService.CreateSessionAsync(userId, source, ct);
        SetSessionCookie(sessionId);
        return sessionId;
    }

    private void SetSessionCookie(Guid sessionId)
    {
        Response.Cookies.Append(SessionCookieName, sessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    private bool TryGetSessionIdFromCookie(out Guid sessionId)
    {
        sessionId = default;
        if (!Request.Cookies.TryGetValue(SessionCookieName, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Guid.TryParse(value, out sessionId);
    }

    private int? GetOptionalUserId()
    {
        if (User.Identity?.IsAuthenticated != true)
            return null;

        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claim, out var userId))
            return userId;

        return null;
    }
}
