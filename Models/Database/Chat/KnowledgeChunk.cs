using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

// ============================================================
// "Mẩu tri thức" nhỏ để bot tìm kiếm.
//
// Ý tưởng dễ hiểu:
// - Một bài FAQ dài được cắt thành vài đoạn ngắn (chunk).
// - Mỗi đoạn được "mã hóa" thành dãy số (embedding) để máy so sánh độ giống.
// - Khi khách hỏi, hệ thống tìm đoạn giống nhất rồi đưa cho AI đọc và trả lời.
//
// Lưu ý: một FAQ/sản phẩm có thể có NHIỀU dòng KnowledgeChunk (nhiều đoạn).
// ============================================================
public class KnowledgeChunk
{
    public long Id { get; set; }

    // Lấy từ FAQ / sản phẩm / cài đặt?
    public KnowledgeSourceType SourceType { get; set; }

    // Id gốc dạng chữ, ví dụ "5" (FAQ id 5) hoặc "contact_phone"
    [Required, MaxLength(64)]
    public string SourceId { get; set; } = string.Empty;

    // Tiêu đề ngắn để dễ nhìn khi debug (thường = tên FAQ / tên SP)
    [MaxLength(300)]
    public string? Title { get; set; }

    // Nội dung chữ của đoạn này
    [Required]
    public string Content { get; set; } = string.Empty;

    // Dãy số embedding lưu dạng JSON, ví dụ: [0.12, -0.05, ...]
    [Required]
    public string EmbeddingJson { get; set; } = "[]";

    // "Dấu vân tay" của nội dung — nếu chữ không đổi thì không cần mã hóa lại (tiết kiệm)
    [Required, MaxLength(64)]
    public string ContentHash { get; set; } = string.Empty;

    // false = đoạn này không còn dùng (FAQ tắt / SP xóa...)
    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
