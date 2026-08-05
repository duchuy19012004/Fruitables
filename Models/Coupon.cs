using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public enum CouponType
{
    Percentage,
    Fixed
}

public class Coupon
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public CouponType Type { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Value { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MinOrderAmount { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal MinQuantity { get; set; } = 1;

    public int? MaxUses { get; set; }

    public int UsedCount { get; set; } = 0;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}
