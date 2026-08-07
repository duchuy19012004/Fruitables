using System.Runtime.CompilerServices;
using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Chat.Intents;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fruitables.Services.Catalog.Products;

namespace Fruitables.Services.Chat.Conversation;

// Điều phối cuộc chat: validate → intent routing → handler phù hợp → lưu DB.
public sealed class ChatService : IChatService
{
    private readonly ApplicationDbContext _db;
    private readonly IRagService _ragService;
    private readonly IIntentRouter _intentRouter;
    private readonly IProductService _productService;
    private readonly IMemoryCache _cache;
    private readonly ChatOptions _options;
    private readonly ILogger<ChatService> _logger;

    // Regex patterns cho sensitive guard
    private static readonly string[] SensitivePatterns = new[]
    {
        "admin", "quản trị", "mật khẩu", "password", "api key", "connection string",
        "debug", "config", "secret", "token", "credential", "database"
    };

    public ChatService(
        ApplicationDbContext db,
        IRagService ragService,
        IIntentRouter intentRouter,
        IProductService productService,
        IMemoryCache cache,
        IOptions<ChatOptions> options,
        ILogger<ChatService> logger)
    {
        _db = db;
        _ragService = ragService;
        _intentRouter = intentRouter;
        _productService = productService;
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

    // Gửi tin nhắn (non-stream): validate → intent routing → handler → lưu DB.
    public async Task<SendChatMessageResponse> SendAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ChatSessionNotFoundException($"Chat session '{sessionId}' was not found.");

        if (userId.HasValue && session.UserId is null)
            session.UserId = userId;

        var trimmed = ValidateMessage(message);

        // Chống spam theo IP
        EnforceRateLimit(sessionId, clientIp);

        // Sensitive guard: chặn trước khi gọi LLM/embedding
        if (IsSensitive(trimmed))
        {
            var safeResponse = "Xin lỗi, mình không thể trả lời câu hỏi liên quan đến hệ thống nội bộ. Bạn có câu hỏi nào về sản phẩm hoặc đơn hàng không?";
            return await SaveAndRespondAsync(session, trimmed, safeResponse, ct);
        }

        // Phân loại intent
        var intent = await _intentRouter.ClassifyAsync(trimmed, ct);
        _logger.LogInformation("Intent classified: {Kind} (confidence={Confidence})", intent.Kind, intent.Confidence);

        if (intent.Kind == ChatIntentKind.GeneralInquiry)
        {
            var answer = await _ragService.AnswerAsync(trimmed, ct);
            return await SaveAndRespondAsync(
                session,
                trimmed,
                answer.Content,
                action: null,
                ct,
                answer.Refused,
                answer.SourceChunkIds);
        }

        // Route theo intent
        var (content, action) = await RouteIntentAsync(intent, trimmed, userId, ct);
        return await SaveAndRespondAsync(session, trimmed, content, action, ct);
    }

    // Streaming: validate → intent routing → handler → stream tokens → lưu DB.
    public async IAsyncEnumerable<ChatStreamEvent> SendStreamingAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ChatSessionNotFoundException($"Chat session '{sessionId}' was not found.");

        if (userId.HasValue && session.UserId is null)
            session.UserId = userId;

        var trimmed = ValidateMessage(message);

        // Chống spam theo IP
        EnforceRateLimit(sessionId, clientIp);

        // Sensitive guard
        if (IsSensitive(trimmed))
        {
            var safeResponse = "Xin lỗi, mình không thể trả lời câu hỏi liên quan đến hệ thống nội bộ. Bạn có câu hỏi nào về sản phẩm hoặc đơn hàng không?";
            var msg = await SaveUserAndAssistantAsync(session, trimmed, safeResponse, null, ct);
            yield return ChatStreamEvent.Meta(sessionId);
            yield return ChatStreamEvent.Token(safeResponse);
            yield return ChatStreamEvent.Done(sessionId, safeResponse, false, msg.Id);
            yield break;
        }

        // Phân loại intent
        var intent = await _intentRouter.ClassifyAsync(trimmed, ct);
        _logger.LogInformation("Intent classified: {Kind} (confidence={Confidence})", intent.Kind, intent.Confidence);

        yield return ChatStreamEvent.Meta(sessionId);

        // Route theo intent
        await foreach (var evt in RouteIntentStreamingAsync(intent, trimmed, userId, session, ct))
        {
            yield return evt;
        }
    }

    // --- Intent routing ---

    private async Task<(string Content, string? Action)> RouteIntentAsync(
        ChatIntent intent, string message, int? userId, CancellationToken ct)
    {
        switch (intent.Kind)
        {
            case ChatIntentKind.OrderStatus:
                return HandleOrderStatus(intent, userId);

            case ChatIntentKind.ProductLookup:
                return await HandleProductLookupAsync(intent);

            case ChatIntentKind.CouponCheck:
                return HandleCouponCheck(intent);

            case ChatIntentKind.ShippingQuote:
                return HandleShippingQuote(intent);

            case ChatIntentKind.OutOfScope:
                return ("Xin lỗi, mình chỉ hỗ trợ các câu hỏi về sản phẩm, đơn hàng và chính sách cửa hàng.", null);

            case ChatIntentKind.SmallTalk:
                return (HandleSmallTalk(intent), null);

            default: // GeneralInquiry → RAG
                var answer = await _ragService.AnswerAsync(message, ct);
                return (answer.Content, null);
        }
    }

    private async IAsyncEnumerable<ChatStreamEvent> RouteIntentStreamingAsync(
        ChatIntent intent, string message, int? userId, ChatSession session,
        [EnumeratorCancellation] CancellationToken ct)
    {
        switch (intent.Kind)
        {
            case ChatIntentKind.OrderStatus:
                var (orderContent, orderAction) = HandleOrderStatus(intent, userId);
                var orderMsg = await SaveUserAndAssistantAsync(session, message, orderContent, orderAction, ct);
                yield return ChatStreamEvent.Token(orderContent);
                yield return ChatStreamEvent.Done(session.Id, orderContent, false, orderMsg.Id, orderAction);
                yield break;

            case ChatIntentKind.ProductLookup:
                var (productContent, productAction) = await HandleProductLookupAsync(intent);
                var productMsg = await SaveUserAndAssistantAsync(session, message, productContent, productAction, ct);
                yield return ChatStreamEvent.Token(productContent);
                yield return ChatStreamEvent.Done(session.Id, productContent, false, productMsg.Id, productAction);
                yield break;

            case ChatIntentKind.CouponCheck:
                var (couponContent, couponAction) = HandleCouponCheck(intent);
                var couponMsg = await SaveUserAndAssistantAsync(session, message, couponContent, couponAction, ct);
                yield return ChatStreamEvent.Token(couponContent);
                yield return ChatStreamEvent.Done(session.Id, couponContent, false, couponMsg.Id, couponAction);
                yield break;

            case ChatIntentKind.ShippingQuote:
                var (shipContent, shipAction) = HandleShippingQuote(intent);
                var shipMsg = await SaveUserAndAssistantAsync(session, message, shipContent, shipAction, ct);
                yield return ChatStreamEvent.Token(shipContent);
                yield return ChatStreamEvent.Done(session.Id, shipContent, false, shipMsg.Id, shipAction);
                yield break;

            case ChatIntentKind.OutOfScope:
                var oosContent = "Xin lỗi, mình chỉ hỗ trợ các câu hỏi về sản phẩm, đơn hàng và chính sách cửa hàng.";
                var oosMsg = await SaveUserAndAssistantAsync(session, message, oosContent, null, ct);
                yield return ChatStreamEvent.Token(oosContent);
                yield return ChatStreamEvent.Done(session.Id, oosContent, false, oosMsg.Id);
                yield break;

            case ChatIntentKind.SmallTalk:
                var smallTalkContent = HandleSmallTalk(intent);
                var smallTalkMsg = await SaveUserAndAssistantAsync(session, message, smallTalkContent, null, ct);
                yield return ChatStreamEvent.Token(smallTalkContent);
                yield return ChatStreamEvent.Done(session.Id, smallTalkContent, false, smallTalkMsg.Id);
                yield break;

            default: // GeneralInquiry → RAG streaming
                string fullContent = string.Empty;
                var refused = false;
                List<long> chunkIds = new();

                await foreach (var part in _ragService.AnswerStreamingAsync(message, ct))
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
                        yield return ChatStreamEvent.Token(part.Text);
                    }
                    else if (part.Kind == "complete")
                    {
                        fullContent = part.Text;
                        refused = false;
                        chunkIds = part.SourceChunkIds ?? new List<long>();
                    }
                }

                var assistantMessage = await SaveUserAndAssistantAsync(
                    session,
                    message,
                    fullContent,
                    action: null,
                    ct,
                    refused,
                    chunkIds,
                    streamed: true);
                yield return ChatStreamEvent.Done(session.Id, fullContent, refused, assistantMessage.Id);
                yield break;
        }
    }

    // --- Intent handlers ---

    private static string HandleSmallTalk(ChatIntent intent)
    {
        var category = intent.Slots.TryGetValue("category", out var value) ? value : "greeting";
        return category switch
        {
            "thanks" => "Không có gì! Mình luôn sẵn sàng hỗ trợ bạn.",
            "apology" => "Không sao nhé! Mình vẫn sẵn sàng hỗ trợ bạn.",
            "goodbye" => "Tạm biệt bạn! Chúc bạn một ngày vui vẻ.",
            "acknowledgement" => "Được nhé! Bạn cần mình hỗ trợ gì thêm không?",
            "capability" => "Mình có thể hỗ trợ về sản phẩm, đơn hàng, phí vận chuyển, mã giảm giá và chính sách cửa hàng.",
            _ => "Chào bạn! Mình có thể hỗ trợ về sản phẩm, đơn hàng và chính sách của Fruitables."
        };
    }

    private (string Content, string? Action) HandleOrderStatus(ChatIntent intent, int? userId)
    {
        if (!userId.HasValue)
        {
            return (
                "Bạn cần đăng nhập để tra cứu đơn hàng. Vào mục Lịch sử đơn hàng sau khi đăng nhập nhé.",
                "login"
            );
        }

        if (intent.Slots.TryGetValue("orderId", out var orderId) && !string.IsNullOrEmpty(orderId))
        {
            return (
                $"Bạn muốn xem đơn hàng #{orderId} đúng không? Vào mục Lịch sử đơn hàng để xem chi tiết nhé.",
                "view_orders"
            );
        }

        return (
            "Vào mục Lịch sử đơn hàng để xem tất cả đơn của bạn. Bạn cần tìm đơn cụ thể nào không?",
            "view_orders"
        );
    }

    private async Task<(string Content, string? Action)> HandleProductLookupAsync(ChatIntent intent)
    {
        if (!intent.Slots.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
        {
            return ("Bạn muốn tìm sản phẩm gì? Cho mình biết tên hoặc loại sản phẩm nhé.", "search");
        }

        // Tìm sản phẩm thực trong DB
        var products = await _productService.GetShopViewModelAsync(null, query, null, null, null, 1, 5);
        if (products.Products != null && products.Products.Any())
        {
            var lines = products.Products.Take(3).Select(p =>
            {
                var price = p.Price.ToString("N0");
                var stock = p.StockQuantity > 0 ? $"Còn {p.StockQuantity} sản phẩm" : "Hết hàng";
                return $"- {p.Name}: {price}đ ({stock})";
            });

            var response = $"Mình tìm thấy sản phẩm \"{query}\":\n{string.Join("\n", lines)}\n\nXem thêm tại trang Tìm kiếm nhé!";
            return (response, "search");
        }

        return (
            $"Xin lỗi, mình không tìm thấy sản phẩm \"{query}\" trong hệ thống. Bạn thử tìm với từ khóa khác nhé!",
            "search"
        );
    }

    private (string Content, string? Action) HandleCouponCheck(ChatIntent intent)
    {
        if (intent.Slots.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code))
        {
            return (
                $"Mã \"{code}\" — bạn nhập mã này ở bước Thanh toán để áp dụng nhé.",
                "checkout"
            );
        }

        return (
            "Bạn có mã giảm giá nào? Nhập ở bước Thanh toán để kiểm tra.",
            "checkout"
        );
    }

    private (string Content, string? Action) HandleShippingQuote(ChatIntent intent)
    {
        if (intent.Slots.TryGetValue("address", out var address) && !string.IsNullOrEmpty(address))
        {
            return (
                $"Phí ship đến \"{address}\" sẽ được tính tự động khi bạn đặt hàng. Xem chi phí ở bước Thanh toán nhé.",
                "checkout"
            );
        }

        return (
            "Phí ship phụ thuộc vào khu vực. Khi đặt hàng, hệ thống sẽ tính phí tự động cho bạn.",
            "checkout"
        );
    }

    // --- Sensitive guard ---

    private static bool IsSensitive(string message)
    {
        var lower = message.ToLowerInvariant();
        return SensitivePatterns.Any(p => lower.Contains(p));
    }

    // --- Validation ---

    private string ValidateMessage(string? message)
    {
        var trimmed = (message ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        if (trimmed.Length > _options.MaxUserMessageChars)
            throw new ArgumentException(
                $"Message exceeds maximum length of {_options.MaxUserMessageChars} characters.",
                nameof(message));

        return trimmed;
    }

    // --- DB helpers ---

    private async Task<SendChatMessageResponse> SaveAndRespondAsync(
        ChatSession session, string userContent, string assistantContent, CancellationToken ct)
    {
        return await SaveAndRespondAsync(session, userContent, assistantContent, null, ct);
    }

    private async Task<SendChatMessageResponse> SaveAndRespondAsync(
        ChatSession session,
        string userContent,
        string assistantContent,
        string? action,
        CancellationToken ct,
        bool refused = false,
        IReadOnlyCollection<long>? chunkIds = null)
    {
        var now = DateTime.UtcNow;

        var assistantCreatedAt = DateTime.UtcNow;
        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = userContent,
            CreatedAt = now
        };
        _db.ChatMessages.Add(userMessage);

        var assistantMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = assistantContent,
            CreatedAt = assistantCreatedAt,
            MetaJson = JsonSerializer.Serialize(new
            {
                refused,
                chunkIds = chunkIds ?? Array.Empty<long>(),
                action
            })
        };
        _db.ChatMessages.Add(assistantMessage);

        AppendMessagesJson(session,
            new Fruitables.Models.Json.ChatMessageDocument { Role = "user", Content = userContent, CreatedAt = now },
            new Fruitables.Models.Json.ChatMessageDocument
            {
                Role = "assistant",
                Content = assistantContent,
                CreatedAt = assistantCreatedAt,
                Metadata = new Fruitables.Models.Json.ChatMessageMetadata { Refused = refused, Action = action }
            });

        session.LastMessageAt = assistantMessage.CreatedAt;
        session.RowVersion = Guid.NewGuid().ToByteArray();
        await _db.SaveChangesAsync(ct);

        return new SendChatMessageResponse
        {
            SessionId = session.Id,
            AssistantMessage = new ChatMessageDto
            {
                Id = assistantMessage.Id,
                Role = assistantMessage.Role,
                Content = assistantMessage.Content,
                CreatedAt = assistantMessage.CreatedAt,
                Refused = refused,
                Action = action
            }
        };
    }

    private async Task<ChatMessage> SaveUserAndAssistantAsync(
        ChatSession session,
        string userContent,
        string assistantContent,
        string? action,
        CancellationToken ct,
        bool refused = false,
        IReadOnlyCollection<long>? chunkIds = null,
        bool streamed = false)
    {
        var now = DateTime.UtcNow;
        var assistantCreatedAt = DateTime.UtcNow;

        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = userContent,
            CreatedAt = now
        };
        _db.ChatMessages.Add(userMessage);

        var assistantMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = assistantContent,
            CreatedAt = assistantCreatedAt,
            MetaJson = JsonSerializer.Serialize(new
            {
                refused,
                chunkIds = chunkIds ?? Array.Empty<long>(),
                streamed,
                action
            })
        };
        _db.ChatMessages.Add(assistantMessage);

        AppendMessagesJson(session,
            new Fruitables.Models.Json.ChatMessageDocument { Role = "user", Content = userContent, CreatedAt = now },
            new Fruitables.Models.Json.ChatMessageDocument
            {
                Role = "assistant",
                Content = assistantContent,
                CreatedAt = assistantCreatedAt,
                Metadata = new Fruitables.Models.Json.ChatMessageMetadata { Refused = refused, Action = action }
            });

        session.LastMessageAt = assistantMessage.CreatedAt;
        session.RowVersion = Guid.NewGuid().ToByteArray();
        await _db.SaveChangesAsync(ct);

        return assistantMessage;
    }

    private void AppendMessagesJson(ChatSession session, params Fruitables.Models.Json.ChatMessageDocument[] messages)
    {
        var serializer = new Fruitables.Services.Infrastructure.Json.VersionedJsonSerializer();
        Fruitables.Models.Json.ChatMessagesDocument document;
        if (string.IsNullOrWhiteSpace(session.MessagesJson) || session.MessagesJson.Trim() == "[]")
            document = new Fruitables.Models.Json.ChatMessagesDocument();
        else
            document = serializer.Deserialize<Fruitables.Models.Json.ChatMessagesDocument>(session.MessagesJson);

        session.MessagesJson = serializer.Serialize(new Fruitables.Models.Json.ChatMessagesDocument
        {
            Messages = [..document.Messages, ..messages]
        });
    }

    private void EnforceRateLimit(Guid sessionId, string? clientIp)
    {
        var ip = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim();
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

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.AsNoTracking().FirstOrDefaultAsync(item => item.Id == sessionId, ct);
        if (session != null &&
            !string.IsNullOrWhiteSpace(session.MessagesJson) &&
            session.MessagesJson.Trim() != "[]")
        {
            try
            {
                var serializer = new Fruitables.Services.Infrastructure.Json.VersionedJsonSerializer();
                var document = serializer.Deserialize<Fruitables.Models.Json.ChatMessagesDocument>(session.MessagesJson);
                if (document.Messages.Count > 0)
                {
                    return document.Messages
                        .Select((message, index) => new ChatMessageDto
                        {
                            Id = index + 1,
                            Role = message.Role,
                            Content = message.Content,
                            CreatedAt = message.CreatedAt,
                            Refused = message.Metadata?.Refused ?? false,
                            Action = message.Metadata?.Action
                        })
                        .ToList();
                }
            }
            catch
            {
                // fall through to legacy rows
            }
        }

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
            Refused = ParseRefused(m.MetaJson),
            Action = ParseAction(m.MetaJson)
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

    public async Task<(List<ChatSessionListItem> Items, int TotalCount)> GetSessionsPageAsync(
        int page, int pageSize, CancellationToken ct = default)
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

    public async Task<ChatSession?> GetSessionWithMessagesAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ChatSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == id, ct);
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
        catch (JsonException) { }

        return false;
    }

    private static string? ParseAction(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            if (doc.RootElement.TryGetProperty("action", out var actionProp)
                && actionProp.ValueKind == JsonValueKind.String)
            {
                return actionProp.GetString();
            }
        }
        catch (JsonException) { }

        return null;
    }
}
