using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

// ============================================================
// Một "cuộc trò chuyện" giữa khách và bot.
// Ví dụ: khách mở widget chat → tạo 1 session → mọi tin nhắn thuộc session đó.
// ============================================================
public class ChatSession
{
    // Mã phiên (chuỗi GUID), lưu trong cookie trình duyệt để nhận ra khách
    public Guid Id { get; set; }

    // Nếu khách đã đăng nhập thì gắn với tài khoản; khách vãng lai = null
    public int? UserId { get; set; }
    public User? User { get; set; }

    // Lúc bắt đầu chat / lúc có tin nhắn gần nhất
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public string MessagesJson { get; set; } = "[]";

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }

    // Chat từ đâu: "widget" (nút góc màn hình) hoặc "page" (trang /Chat)
    [MaxLength(20)]
    public string? Source { get; set; }

    // Danh sách tin nhắn trong cuộc chat này
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
