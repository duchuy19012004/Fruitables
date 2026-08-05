using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Chat.Conversation;

// ============================================================
// Nghiệp vụ chat: tạo cuộc hội thoại, gửi tin, xem lịch sử, admin xem log.
// ============================================================
public interface IChatService
{
    // Bắt đầu một cuộc chat mới (có thể gắn user đã login)
    Task<Guid> CreateSessionAsync(int? userId, string? source, CancellationToken ct = default);

    // Khách gửi 1 tin → bot trả lời (có kiểm tra độ dài + chống spam)
    Task<SendChatMessageResponse> SendAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        CancellationToken ct = default);

    // Giống SendAsync nhưng stream token (SSE) khi AI đang generate
    IAsyncEnumerable<ChatStreamEvent> SendStreamingAsync(
        Guid sessionId,
        string message,
        int? userId,
        string? clientIp,
        CancellationToken ct = default);

    // Lấy toàn bộ tin nhắn của 1 cuộc chat
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken ct = default);

    // Khách login giữa chừng → gắn account vào session đang mở
    Task AttachUserAsync(Guid sessionId, int userId, CancellationToken ct = default);

    // Admin: danh sách cuộc chat (phân trang)
    Task<(List<ChatSessionListItem> Items, int TotalCount)> GetSessionsPageAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);

    // Admin: xem chi tiết 1 cuộc chat kèm tin nhắn
    Task<ChatSession?> GetSessionWithMessagesAsync(Guid id, CancellationToken ct = default);
}
