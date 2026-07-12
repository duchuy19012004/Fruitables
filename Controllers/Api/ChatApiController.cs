using System.Security.Claims;
using Fruitables.Services.Chat;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Controllers.Api;

[ApiController]
[Route("api/chat")]
public class ChatApiController : ControllerBase
{
    public const string SessionCookieName = "chat_session_id";

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
