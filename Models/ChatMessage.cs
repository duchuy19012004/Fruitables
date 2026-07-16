using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

// ============================================================
// Một tin nhắn trong cuộc chat (của khách hoặc của bot).
// ============================================================
public class ChatMessage
{
    public long Id { get; set; }

    // Thuộc cuộc chat nào
    public Guid SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;

    // Ai nói: "user" = khách, "assistant" = bot
    [Required, MaxLength(20)]
    public string Role { get; set; } = "user";

    // Nội dung tin nhắn (chữ)
    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Thông tin thêm dạng JSON, ví dụ bot có "từ chối trả lời" không
    // (không hiện ra cho khách, dùng để admin/debug)
    public string? MetaJson { get; set; }
}
