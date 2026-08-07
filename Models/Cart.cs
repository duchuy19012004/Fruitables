using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class Cart
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    [MaxLength(255)]
    public string? SessionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Mã giảm giá đang áp dụng
    [MaxLength(50)]
    public string? CouponCode { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(10,2)")]
    public decimal CouponDiscount { get; set; } = 0;

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    public virtual ICollection<CartGroup> Groups { get; set; } = new List<CartGroup>();
}
