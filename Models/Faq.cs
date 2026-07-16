using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

// ============================================================
// FAQ = Câu hỏi thường gặp (do nhân viên CS viết trong Admin)
// Bot chat sẽ đọc các bài FAQ này để trả lời khách.
// ============================================================
public class Faq
{
    // Mã số tự tăng trong database
    public int Id { get; set; }

    // Tiêu đề ngắn, ví dụ: "Phí vận chuyển như thế nào?"
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Nội dung trả lời chi tiết (văn bản thường)
    [Required]
    public string Body { get; set; } = string.Empty;

    // Nhóm chủ đề để dễ lọc: shipping, payment, hours, ...
    [MaxLength(50)]
    public string Category { get; set; } = "general";

    // true = đang dùng (bot được phép học); false = ẩn, bot không dùng
    public bool IsActive { get; set; } = true;

    // Thời điểm tạo / sửa lần cuối (giờ UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
