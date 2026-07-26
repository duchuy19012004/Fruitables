using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public class CartGroup
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ComboId { get; set; }
    public int ComboRevision { get; set; }
    public int Quantity { get; set; } = 1;

    [Required, MaxLength(255)]
    public string ComboName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    public decimal OriginalTotal { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal FinalTotal { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Discount { get; set; }

    public bool AllowCouponStacking { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    public virtual Cart Cart { get; set; } = null!;
    public virtual Combo Combo { get; set; } = null!;
    public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
