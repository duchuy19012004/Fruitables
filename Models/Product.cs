using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ShortDescription { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    [NotMapped]
    public decimal? SalePrice { get; set; }

    [NotMapped]
    public decimal DisplayMinPrice { get; set; }

    [NotMapped]
    public decimal DisplayMaxPrice { get; set; }

    [NotMapped]
    public bool HasPriceRange => DisplayMaxPrice > DisplayMinPrice;

    [MaxLength(20)]
    public string Unit { get; set; } = "kg";

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Weight { get; set; }

    [MaxLength(100)]
    public string? CountryOrigin { get; set; }

    [MaxLength(50)]
    public string? Quality { get; set; }

    public int StockQuantity { get; set; } = 0;

    public int MinOrderQuantity { get; set; } = 1;

    public bool IsFeatured { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    // Review statistics
    [Column(TypeName = "decimal(3,2)")]
    public decimal AverageRating { get; set; } = 0;
    public int ReviewCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<ProductTag> Tags { get; set; } = new List<ProductTag>();
    public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public virtual ICollection<PriceSchedule> PriceSchedules { get; set; } = new List<PriceSchedule>();
}
