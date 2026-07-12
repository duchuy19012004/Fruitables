using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat;

public sealed class ChatService : IChatService
{
    private readonly ApplicationDbContext _db;
    private readonly IRagService _ragService;
    private readonly IMemoryCache _cache;
    private readonly ChatOptions _options;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext db,
        IRagService ragService,
        IMemoryCache cache,
        IOptions<ChatOptions> options,
        ILogger<ChatService> logger)
    {
        _db = db;
        _ragService = ragService;
        _cache = cache;
        _options = options?.Value ?? new ChatOptions();
        _logger = logger;
    }

    public async Task<Guid> CreateSessionAsync(int? userId, string? source, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = source,
            CreatedAt = now,
            LastMessageAt = now
        };

        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task<SendChatMessageResponse> SendAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException($"Chat session '{sessionId}' was not found.");

        if (userId.HasValue && session.UserId is null)
        {
            session.UserId = userId;
        }

        var trimmed = (message ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        if (trimmed.Length > _options.MaxUserMessageChars)
            throw new ArgumentException(
                $"Message exceeds maximum length of {_options.MaxUserMessageChars} characters.",
                nameof(message));

        EnforceRateLimit(sessionId, clientIp);

        var now = DateTime.UtcNow;

        var userMessage = new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = trimmed,
            CreatedAt = now
        };
        _db.ChatMessages.Add(userMessage);

        var answer = await _ragService.AnswerAsync(trimmed, ct);

        var assistantMessage = new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = answer.Content,
            CreatedAt = DateTime.UtcNow,
            MetaJson = JsonSerializer.Serialize(new
            {
                refused = answer.Refused,
                chunkIds = answer.SourceChunkIds
            })
        };
        _db.ChatMessages.Add(assistantMessage);

        session.LastMessageAt = assistantMessage.CreatedAt;
        await _db.SaveChangesAsync(ct);

        return new SendChatMessageResponse
        {
            SessionId = sessionId,
            AssistantMessage = new ChatMessageDto
            {
                Id = assistantMessage.Id,
                Role = assistantMessage.Role,
                Content = assistantMessage.Content,
                CreatedAt = assistantMessage.CreatedAt,
                Refused = answer.Refused
            }
        };
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            Refused = ParseRefused(m.MetaJson)
        }).ToList();
    }

    public async Task AttachUserAsync(Guid sessionId, int userId, CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.UserId is not null)
            return;

        session.UserId = userId;
        await _db.SaveChangesAsync(ct);
    }

    private void EnforceRateLimit(Guid sessionId, string? clientIp)
    {
        var ip = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim();
        var bucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var key = $"chat-rl:{ip}:{sessionId}:{bucket}";

        var count = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return 0;
        });

        count++;
        _cache.Set(key, count, TimeSpan.FromMinutes(2));

        if (count > _options.RateLimitPerMinute)
        {
            _logger.LogWarning(
                "Chat rate limit exceeded for session {SessionId} from {ClientIp} ({Count}/{Limit})",
                sessionId, ip, count, _options.RateLimitPerMinute);
            throw new ChatRateLimitException(
                $"Rate limit exceeded. Maximum {_options.RateLimitPerMinute} messages per minute.");
        }
    }

    private static bool ParseRefused(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            if (doc.RootElement.TryGetProperty("refused", out var refusedProp)
                && (refusedProp.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                return refusedProp.GetBoolean();
            }
        }
        catch (JsonException)
        {
            // ignore malformed meta
        }

        return false;
    }
}
