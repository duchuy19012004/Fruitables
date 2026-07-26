using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.ViewModels;

public class CategoryListViewModel
{
    public List<CategoryTreeNode> Categories { get; set; } = new();
}

public class CreateCategoryViewModel
{
    [Required(ErrorMessage = "Tên danh mục không được để trống")]
    [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Slug không được vượt quá 100 ký tự")]
    public string? Slug { get; set; }

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
    public string? Description { get; set; }

    [StringLength(255, ErrorMessage = "Đường dẫn hình ảnh không được vượt quá 255 ký tự")]
    public string? Image { get; set; }

    public int? ParentId { get; set; }

    public bool IsActive { get; set; } = true;

    public List<CategoryTreeNode> ParentCategories { get; set; } = new();
}

public class EditCategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên danh mục không được để trống")]
    [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Slug không được vượt quá 100 ký tự")]
    public string? Slug { get; set; }

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
    public string? Description { get; set; }

    [StringLength(255, ErrorMessage = "Đường dẫn hình ảnh không được vượt quá 255 ký tự")]
    public string? Image { get; set; }

    public int? ParentId { get; set; }

    public bool IsActive { get; set; }

    public List<CategoryTreeNode> ParentCategories { get; set; } = new();
}

public class ReorderCategoriesRequest
{
    public int? ParentId { get; set; }
    public List<int> CategoryIds { get; set; } = new();
}

public class MoveCategoryRequest
{
    public int CategoryId { get; set; }
    public int? NewParentId { get; set; }
}
