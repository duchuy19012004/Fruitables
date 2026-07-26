namespace Fruitables.ViewModels;

// ============================================================
// "Hộp dữ liệu" trao đổi giữa API / service / giao diện chat
// (không phải bảng DB — chỉ để vận chuyển thông tin)
// ============================================================

// Kết quả bot sau khi suy nghĩ (RAG)
public class RagAnswer
{
    // Câu trả lời chữ
    public string Content { get; set; } = string.Empty;

    // true = bot chủ động nói "chưa có thông tin"
    public bool Refused { get; set; }

    // Id các mẩu tri thức đã dùng (debug / meta)
    public List<long> SourceChunkIds { get; set; } = new();
}

// Một phần stream từ RagService (nội bộ → ChatService)
public class RagStreamPart
{
    /// <summary>token | refused | complete</summary>
    public string Kind { get; set; } = string.Empty;

    // Token delta, hoặc full text khi refused/complete
    public string Text { get; set; } = string.Empty;

    public bool Refused { get; set; }

    public List<long> SourceChunkIds { get; set; } = new();

    public static RagStreamPart Token(string delta) => new()
    {
        Kind = "token",
        Text = delta
    };

    public static RagStreamPart Refuse(string content) => new()
    {
        Kind = "refused",
        Text = content,
        Refused = true,
        SourceChunkIds = new List<long>()
    };

    public static RagStreamPart Complete(string fullContent, List<long> chunkIds) => new()
    {
        Kind = "complete",
        Text = fullContent,
        Refused = false,
        SourceChunkIds = chunkIds ?? new List<long>()
    };
}

// Sự kiện SSE ra trình duyệt: meta | token | done | error
public class ChatStreamEvent
{
    public string Type { get; set; } = string.Empty;

    public Guid? SessionId { get; set; }

    // Mảnh chữ (token) hoặc full content (done)
    public string? Text { get; set; }

    public bool? Refused { get; set; }

    public long? MessageId { get; set; }

    public string? Action { get; set; }

    public string? Error { get; set; }

    public static ChatStreamEvent Meta(Guid sessionId) => new()
    {
        Type = "meta",
        SessionId = sessionId
    };

    public static ChatStreamEvent Token(string delta) => new()
    {
        Type = "token",
        Text = delta
    };

    public static ChatStreamEvent Done(Guid sessionId, string content, bool refused, long messageId, string? action = null) => new()
    {
        Type = "done",
        SessionId = sessionId,
        Text = content,
        Refused = refused,
        MessageId = messageId,
        Action = action
    };

    public static ChatStreamEvent Fail(string error) => new()
    {
        Type = "error",
        Error = error
    };
}

// Body khi tạo session: từ widget hay trang full
public class CreateChatSessionRequest
{
    public string? Source { get; set; }
}

// Body khi khách gửi tin
public class SendChatMessageRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
}

// 1 tin nhắn trả ra JSON cho giao diện
public class ChatMessageDto
{
    public long Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Refused { get; set; }
    public string? Action { get; set; }
}

// Phản hồi sau khi gửi tin: id cuộc chat + tin bot
public class SendChatMessageResponse
{
    public Guid SessionId { get; set; }
    public ChatMessageDto AssistantMessage { get; set; } = null!;
}

// 1 dòng trong bảng Admin "Chat logs"
public class ChatSessionListItem
{
    public Guid Id { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public int MessageCount { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string? Source { get; set; }
}
