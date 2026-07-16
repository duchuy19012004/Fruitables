using System.Runtime.CompilerServices;
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

// ============================================================
// ĐIỀU PHỐI CUỘC CHAT
//
// Việc của lớp này (không phải "suy nghĩ" trả lời — việc đó do RagService):
// - Tạo / tìm cuộc chat
// - Kiểm tra tin nhắn (rỗng? quá dài?)
// - Chống spam theo IP
// - Lưu tin khách + tin bot vào DB
// - Cung cấp log cho Admin
// ============================================================
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

    // Mở một cuộc chat mới
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

    // Khách gửi tin → bot trả lời
    public async Task<SendChatMessageResponse> SendAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        CancellationToken ct = default)
    {
        // Cuộc chat phải còn tồn tại
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ChatSessionNotFoundException($"Chat session '{sessionId}' was not found.");

        // Khách vừa login → gắn account (nếu session trước đó là khách vãng lai)
        if (userId.HasValue && session.UserId is null)
        {
            session.UserId = userId;
        }

        // Kiểm tra nội dung
        var trimmed = (message ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        if (trimmed.Length > _options.MaxUserMessageChars)
            throw new ArgumentException(
                $"Message exceeds maximum length of {_options.MaxUserMessageChars} characters.",
                nameof(message));

        // Chống gửi liên tục (tính theo IP)
        EnforceRateLimit(sessionId, clientIp);

        var now = DateTime.UtcNow;

        // Lưu tin của khách
        var userMessage = new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = trimmed,
            CreatedAt = now
        };
        _db.ChatMessages.Add(userMessage);

        // Nhờ RAG tìm tri thức + gọi AI
        var answer = await _ragService.AnswerAsync(trimmed, ct);

        // Lưu tin của bot (kèm meta: có từ chối không, lấy từ mẩu tri thức nào)
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

    // Stream: validate → lưu tin user → stream token → lưu tin bot → done
    public async IAsyncEnumerable<ChatStreamEvent> SendStreamingAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Cuộc chat phải còn tồn tại
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ChatSessionNotFoundException($"Chat session '{sessionId}' was not found.");

        if (userId.HasValue && session.UserId is null)
            session.UserId = userId;

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
        session.LastMessageAt = now;
        await _db.SaveChangesAsync(ct);

        yield return ChatStreamEvent.Meta(sessionId);

        string fullContent = string.Empty;
        var refused = false;
        List<long> chunkIds = new();

        await foreach (var part in _ragService.AnswerStreamingAsync(trimmed, ct))
        {
            if (part.Kind == "token")
            {
                yield return ChatStreamEvent.Token(part.Text);
            }
            else if (part.Kind == "refused")
            {
                fullContent = part.Text;
                refused = true;
                chunkIds = part.SourceChunkIds ?? new List<long>();
                // Gửi full refuse text như token để UI hiển thị ngay
                yield return ChatStreamEvent.Token(part.Text);
            }
            else if (part.Kind == "complete")
            {
                fullContent = part.Text;
                refused = false;
                chunkIds = part.SourceChunkIds ?? new List<long>();
            }
        }

        var assistantMessage = new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = fullContent,
            CreatedAt = DateTime.UtcNow,
            MetaJson = JsonSerializer.Serialize(new
            {
                refused,
                chunkIds,
                streamed = true
            })
        };
        _db.ChatMessages.Add(assistantMessage);
        session.LastMessageAt = assistantMessage.CreatedAt;
        await _db.SaveChangesAsync(ct);

        yield return ChatStreamEvent.Done(sessionId, fullContent, refused, assistantMessage.Id);
    }

    // Lịch sử tin nhắn (cho widget / trang chat)
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

    // Gắn user vào session guest
    public async Task AttachUserAsync(Guid sessionId, int userId, CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.UserId is not null)
            return;

        session.UserId = userId;
        await _db.SaveChangesAsync(ct);
    }

    // Admin: danh sách cuộc chat, mới nhất trước
    public async Task<(List<ChatSessionListItem> Items, int TotalCount)> GetSessionsPageAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _db.ChatSessions.AsNoTracking();
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ChatSessionListItem
            {
                Id = s.Id,
                UserId = s.UserId,
                UserEmail = s.User != null ? s.User.Email : null,
                MessageCount = s.Messages.Count,
                LastMessageAt = s.LastMessageAt,
                Source = s.Source
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }

    // Admin: xem full 1 cuộc chat
    public async Task<ChatSession?> GetSessionWithMessagesAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ChatSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    // Đếm số tin theo IP mỗi phút; vượt ngưỡng → chặn
    private void EnforceRateLimit(Guid sessionId, string? clientIp)
    {
        var ip = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim();
        // "Thùng" theo phút: 202607121430 = phút 14:30 ngày đó
        var bucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var key = $"chat-rl:{ip}:{bucket}";

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

    // Đọc cờ "bot đã từ chối" từ MetaJson
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
            // Meta hỏng thì coi như không từ chối
        }

        return false;
    }
}
