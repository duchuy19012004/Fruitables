namespace Fruitables.ViewModels;

public class RagAnswer
{
    public string Content { get; set; } = string.Empty;
    public bool Refused { get; set; }
    public List<long> SourceChunkIds { get; set; } = new();
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
