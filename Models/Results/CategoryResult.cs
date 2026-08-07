namespace Fruitables.Models;

public class CategoryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Category? Category { get; set; }
    public CategoryErrorType? ErrorType { get; set; }

    public static CategoryResult Ok(Category category) => new()
    {
        Success = true,
        Category = category
    };

    public static CategoryResult Fail(CategoryErrorType errorType, string message) => new()
    {
        Success = false,
        ErrorType = errorType,
        ErrorMessage = message
    };
}

public enum CategoryErrorType
{
    ValidationError,
    NotFound,
    DuplicateSlug,
    CircularReference,
    HasProducts,
    HasChildren,
    InvalidParent
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CategoryTreeNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public int Level { get; set; }
    public int? ParentId { get; set; }
    public List<CategoryTreeNode> Children { get; set; } = new();
}
