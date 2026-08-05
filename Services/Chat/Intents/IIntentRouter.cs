namespace Fruitables.Services.Chat.Intents;

// Phân loại ý định khách hàng: tin nhắn → ChatIntent.
public interface IIntentRouter
{
    Task<ChatIntent> ClassifyAsync(string userMessage, CancellationToken ct = default);
}
