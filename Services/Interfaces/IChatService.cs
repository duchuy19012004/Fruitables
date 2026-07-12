using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface IChatService
{
    Task<Guid> CreateSessionAsync(int? userId, string? source, CancellationToken ct = default);

    Task<SendChatMessageResponse> SendAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        CancellationToken ct = default);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default);

    Task AttachUserAsync(Guid sessionId, int userId, CancellationToken ct = default);
}
