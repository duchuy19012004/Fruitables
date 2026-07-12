namespace Fruitables.ViewModels;

public class RagAnswer
{
    public string Content { get; set; } = string.Empty;
    public bool Refused { get; set; }
    public List<long> SourceChunkIds { get; set; } = new();
}

public class CreateChatSessionRequest
{
    public string? Source { get; set; }
}

public class SendChatMessageRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
}

public class ChatMessageDto
{
    public long Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Refused { get; set; }
}

public class SendChatMessageResponse
{
    public Guid SessionId { get; set; }
    public ChatMessageDto AssistantMessage { get; set; } = null!;
}

public class ChatSessionListItem
{
    public Guid Id { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public int MessageCount { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string? Source { get; set; }
}
