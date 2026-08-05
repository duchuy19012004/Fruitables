using Fruitables.Models;

namespace Fruitables.Services.Catalog.Categories;

public interface ICategoryService
{
    // Existing methods
    Task<List<Category>> GetAllCategoriesAsync();
    Task<Category?> GetCategoryByIdAsync(int id);
    Task<Category?> GetCategoryBySlugAsync(string slug);
    Task<List<Category>> GetParentCategoriesAsync();

    // CRUD methods
    Task<CategoryResult> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryResult> UpdateCategoryAsync(int id, UpdateCategoryRequest request);
    Task<CategoryResult> DeleteCategoryAsync(int id);

    // Hierarchy methods
    Task<List<CategoryTreeNode>> GetCategoryTreeAsync();
    Task<bool> IsDescendantOfAsync(int categoryId, int potentialAncestorId);
    Task<CategoryResult> MoveCategoryAsync(int categoryId, int? newParentId);

    // Sorting methods
    Task<CategoryResult> UpdateSortOrderAsync(int categoryId, int newSortOrder);
    Task<CategoryResult> ReorderCategoriesAsync(int? parentId, List<int> categoryIds);

    // Soft Delete & Restore methods
    Task<CategoryResult> SoftDeleteCategoryAsync(int id);
    Task<CategoryResult> RestoreCategoryAsync(int id);
    Task<List<Category>> GetDeletedCategoriesAsync();
}
