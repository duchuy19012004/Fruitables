namespace Fruitables.Models;

/// <summary>
/// Lưu trữ thông tin user đã bấm "Hữu ích" cho review nào,
/// ngăn user vote nhiều lần cho cùng 1 review.
/// </summary>
public class ReviewHelpful
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Review Review { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
