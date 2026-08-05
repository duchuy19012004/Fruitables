using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Fruitables.Models;

namespace Fruitables.ViewModels;

// ========== Admin form models ==========

public class ComboItemFormModel
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int? ProductVariantId { get; set; }

    [Range(typeof(decimal), "0.1", "1000000", ErrorMessage = "Số lượng phải lớn hơn 0")]
    public decimal Quantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;
}

public class ComboFormViewModel
{
    public int Id { get; set; }
    public int Revision { get; set; }

    [Required(ErrorMessage = "Tên combo không được để trống")]
    [StringLength(255, ErrorMessage = "Tên combo không được vượt quá 255 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Slug không được vượt quá 255 ký tự")]
    public string? Slug { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Hình ảnh (URL)")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Hình ảnh")]
    [DataType(DataType.Upload)]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Vòng đời")]
    public ComboLifecycleStatus Status { get; set; } = ComboLifecycleStatus.Active;

    [Display(Name = "Bắt đầu bán")]
    public DateTimeOffset? StartsAt { get; set; }

    [Display(Name = "Kết thúc bán")]
    public DateTimeOffset? EndsAt { get; set; }

    [Display(Name = "Cách tính giá")]
    public ComboPricingType PricingType { get; set; } = ComboPricingType.SumOfItems;

    [Range(0.01, double.MaxValue, ErrorMessage = "Giá combo phải lớn hơn 0")]
    public decimal? FixedPrice { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Mức giảm phải lớn hơn 0")]
    public decimal? DiscountValue { get; set; }

    [Display(Name = "Cho phép dùng thêm coupon")]
    public bool AllowCouponStacking { get; set; } = true;

    [Display(Name = "Thứ tự hiển thị")]
    public int SortOrder { get; set; } = 0;

    public List<ComboItemFormModel> Items { get; set; } = new();

    // Dropdown data for admin views
    public List<ComboProductOptionViewModel> Products { get; set; } = new();
}

public class CreateComboViewModel : ComboFormViewModel { }

public class EditComboViewModel : ComboFormViewModel { }

public class ComboProductOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    public decimal StockQuantity { get; set; }
    public List<ComboVariantOptionViewModel> Variants { get; set; } = new();
}

public class ComboVariantOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal StockQuantity { get; set; }
}

// ========== Admin list models ==========

public class ComboListViewModel
{
    public List<ComboListRowViewModel> Items { get; set; } = new();
}

public class ComboListRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal Savings { get; set; }
    public ComboPricingType PricingType { get; set; }
    public bool IsActive { get; set; }
    public bool IsSellable { get; set; }
    public ComboLifecycleStatus Status { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}

public class ComboAuditRowViewModel
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string? Details { get; set; }
    public string AdminName { get; set; } = "Hệ thống";
    public DateTime CreatedAt { get; set; }
}

public class ComboAuditViewModel
{
    public int ComboId { get; set; }
    public string ComboName { get; set; } = string.Empty;
    public List<ComboAuditRowViewModel> Items { get; set; } = new();
}

public class ComboReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<ComboReportRowViewModel> Rows { get; set; } = new();
    public decimal NetRevenue => Rows.Sum(row => row.NetRevenue);
    public int BundlesSold => Rows.Sum(row => row.BundlesSold);
}

public class ComboReportRowViewModel
{
    public int ComboId { get; set; }
    public string ComboName { get; set; } = string.Empty;
    public int BundlesSold { get; set; }
    public int OrderCount { get; set; }
    public decimal ComboDiscount { get; set; }
    public decimal DeliveredRevenue { get; set; }
    public decimal RefundedRevenue { get; set; }
    public decimal NetRevenue => DeliveredRevenue - RefundedRevenue;
}

// ========== Storefront card models ==========

public class ComboCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal Savings { get; set; }
    public bool AllowCouponStacking { get; set; }
    public int Revision { get; set; }
    public List<ComboCardItemViewModel> Items { get; set; } = new();
}

public class ComboCardItemViewModel
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? VariantName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsAvailable { get; set; }
    public string UnavailableReason { get; set; } = string.Empty;
}

// ========== Add-to-cart result ==========

public class AddComboToCartResult
{
    public bool Success { get; set; }
    public int AddedCount { get; set; }
    public List<string> SkippedItems { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
