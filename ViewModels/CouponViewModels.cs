using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.ViewModels;

public class CouponApplyResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public string? Message { get; set; }
}

public class CouponEligibilityResult
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public CouponType Type { get; set; }
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public decimal MinQuantity { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsEligible { get; set; }
    public string? IneligibleReason { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class CreateCouponRequest
{
    [Required(ErrorMessage = "Mã giảm giá không được trống")]
    [MaxLength(50)]
    [RegularExpression(@"^[A-Z0-9_\-]+$", ErrorMessage = "Chỉ dùng chữ in hoa, số, '_', '-'")]
    public string Code { get; set; } = string.Empty;

    [Required]
    public CouponType Type { get; set; } = CouponType.Fixed;

    [Required]
    [Range(0.01, 100000000, ErrorMessage = "Giá trị phải lớn hơn 0")]
    public decimal Value { get; set; }

    [Range(0, 100000000, ErrorMessage = "Giá trị đơn hàng tối thiểu không hợp lệ")]
    public decimal MinOrderAmount { get; set; } = 0;

    [Range(typeof(decimal), "0.1", "1000000", ErrorMessage = "Số lượng sản phẩm tối thiểu phải lớn hơn 0")]
    public decimal MinQuantity { get; set; } = 1;

    public int? MaxUses { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateCouponRequest : CreateCouponRequest { }

public class CouponListViewModel
{
    public List<Coupon> Coupons { get; set; } = new();
}

public class CreateCouponViewModel : CreateCouponRequest { }

public class EditCouponViewModel : UpdateCouponRequest
{
    public int Id { get; set; }
    public int UsedCount { get; set; }
}
