using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [Required, MaxLength(50)]
    public string SKU { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    public int PriceRevision { get; set; } = 1;

    [NotMapped]
    public decimal? SalePrice { get; set; }

    [NotMapped]
    public decimal DisplayPrice { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal StockQuantity { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual Product Product { get; set; } = null!;
    public virtual ICollection<PriceSchedule> PriceSchedules { get; set; } = new List<PriceSchedule>();
}
