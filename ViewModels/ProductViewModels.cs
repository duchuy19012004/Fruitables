using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.ViewModels;

#region Request DTOs

public class ProductFormRequest
{
    [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
    [StringLength(255, ErrorMessage = "Tên sản phẩm không được vượt quá 255 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Slug không được vượt quá 255 ký tự")]
    public string? Slug { get; set; }

    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Mô tả ngắn không được vượt quá 500 ký tự")]
    public string? ShortDescription { get; set; }

    [Required(ErrorMessage = "Danh mục không được để trống")]
    public int CategoryId { get; set; }

    [StringLength(20, ErrorMessage = "Đơn vị không được vượt quá 20 ký tự")]
    public string Unit { get; set; } = "kg";

    [Range(0, double.MaxValue, ErrorMessage = "Trọng lượng phải lớn hơn hoặc bằng 0")]
    public decimal? Weight { get; set; }

    [StringLength(100, ErrorMessage = "Xuất xứ không được vượt quá 100 ký tự")]
    public string? CountryOrigin { get; set; }

    [StringLength(50, ErrorMessage = "Chất lượng không được vượt quá 50 ký tự")]
    public string? Quality { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho phải lớn hơn hoặc bằng 0")]
    public int StockQuantity { get; set; } = 0;

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng đặt hàng tối thiểu phải lớn hơn 0")]
    public int MinOrderQuantity { get; set; } = 1;

    public bool IsFeatured { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public List<string>? Tags { get; set; }
}

public class CreateProductRequest : ProductFormRequest
{
    [Required(ErrorMessage = "Giá không được để trống")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn hoặc bằng 0")]
    public decimal Price { get; set; }
}

public class UpdateProductRequest : ProductFormRequest
{
    public int Id { get; set; }
}

public class ProductListRequest
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? SortBy { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public bool IncludeDeleted { get; set; } = false;
}

public class UpdateVariantRequest
{
    [Required(ErrorMessage = "SKU không được để trống")]
    [StringLength(50, ErrorMessage = "SKU không được vượt quá 50 ký tự")]
    public string SKU { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên biến thể không được để trống")]
    [StringLength(100, ErrorMessage = "Tên biến thể không được vượt quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho phải lớn hơn hoặc bằng 0")]
    public int StockQuantity { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

public class CreateVariantRequest : UpdateVariantRequest
{
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Giá không được để trống")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn hoặc bằng 0")]
    public decimal Price { get; set; }
}

#endregion

#region Response DTOs

public class ProductListResult
{
    public List<Product> Products { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
}

#endregion

#region View Models

public class ProductListViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? SortBy { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
}

public class CreateProductViewModel
{
    public CreateProductRequest Product { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
}

public class EditProductViewModel
{
    public Product Product { get; set; } = null!;
    public List<Category> Categories { get; set; } = new();
    public List<ProductTag> AllTags { get; set; } = new();
}

#endregion
