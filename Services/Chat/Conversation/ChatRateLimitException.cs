namespace Fruitables.Services.Chat.Conversation;

// Lỗi khi khách gửi tin quá nhanh (vượt giới hạn tin/phút).
// Controller sẽ trả HTTP 429 và hiện thông báo thân thiện.
public class ChatRateLimitException : Exception
{
    public ChatRateLimitException(string message) : base(message)
    {
    }
}
