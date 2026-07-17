using System.ComponentModel.DataAnnotations;

namespace Fruitables.ViewModels;

// ========== Admin form models ==========

public class ComboItemFormModel
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int? ProductVariantId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 1")]
    public int Quantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;
}

public class ComboFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên combo không được để trống")]
    [StringLength(255, ErrorMessage = "Tên combo không được vượt quá 255 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Slug không được vượt quá 255 ký tự")]
    public string? Slug { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Hình ảnh (URL)")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;

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
    public List<ComboVariantOptionViewModel> Variants { get; set; } = new();
}

public class ComboVariantOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
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
    public bool IsActive { get; set; }
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
    public List<ComboCardItemViewModel> Items { get; set; } = new();
}

public class ComboCardItemViewModel
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
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
