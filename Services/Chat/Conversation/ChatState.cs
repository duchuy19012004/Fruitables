namespace Fruitables.Services.Chat.Conversation;

// Trạng thái per-connection — lưu trong Hub.Context.Items, không lưu DB.
public sealed class ChatState
{
    public Guid? SessionId { get; set; }
    public string? LastUserMessage { get; set; }
    public string? LastIntent { get; set; }
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; }

    // Lấy hoặc tạo ChatState từ Hub context (mỗi kết nối = 1 instance).
    public static ChatState GetOrAdd(Microsoft.AspNetCore.SignalR.HubCallerContext context)
    {
        const string key = "ChatState";

        if (context.Items.TryGetValue(key, out var obj) && obj is ChatState state)
        {
            state.LastActiveAt = DateTime.UtcNow;
            return state;
        }

        var newState = new ChatState();
        context.Items[key] = newState;
        return newState;
    }
}
