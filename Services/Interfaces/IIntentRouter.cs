using Fruitables.Services.Chat.Intents;

namespace Fruitables.Services.Interfaces;

// Phân loại ý định khách hàng: tin nhắn → ChatIntent.
public interface IIntentRouter
{
    Task<ChatIntent> ClassifyAsync(string userMessage, CancellationToken ct = default);
}
