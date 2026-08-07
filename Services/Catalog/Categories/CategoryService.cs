using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Catalog.Products;

namespace Fruitables.Services.Catalog.Categories;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    #region Existing Methods

    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        return await _unitOfWork.Categories.GetAllWithProductsAsync();
    }

    public async Task<Category?> GetCategoryByIdAsync(int id)
    {
        return await _unitOfWork.Categories.GetByIdWithAllAsync(id);
    }

    public async Task<Category?> GetCategoryBySlugAsync(string slug)
    {
        return await _unitOfWork.Categories.GetBySlugAsync(slug);
    }

    public async Task<List<Category>> GetParentCategoriesAsync()
    {
        return await _unitOfWork.Categories.GetRootCategoriesAsync();
    }

    #endregion

    #region Helper Methods

    private static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var slug = name.ToLowerInvariant();
        
        slug = slug.Replace("đ", "d").Replace("Đ", "d");
        slug = Regex.Replace(slug, "[àáạảãâầấậẩẫăằắặẳẵ]", "a");
        slug = Regex.Replace(slug, "[èéẹẻẽêềếệểễ]", "e");
        slug = Regex.Replace(slug, "[ìíịỉĩ]", "i");
        slug = Regex.Replace(slug, "[òóọỏõôồốộổỗơờớợởỡ]", "o");
        slug = Regex.Replace(slug, "[ùúụủũưừứựửữ]", "u");
        slug = Regex.Replace(slug, "[ỳýỵỷỹ]", "y");
        
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        return slug;
    }

    public async Task<bool> IsDescendantOfAsync(int categoryId, int potentialAncestorId)
    {
        if (categoryId == potentialAncestorId)
            return false;

        var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
        if (category == null)
            return false;

        var currentParentId = category.ParentId;
        var visited = new HashSet<int> { categoryId };

        while (currentParentId.HasValue)
        {
            if (currentParentId.Value == potentialAncestorId)
                return true;

            if (visited.Contains(currentParentId.Value))
                break;

            visited.Add(currentParentId.Value);
            var parent = await _unitOfWork.Categories.GetByIdAsync(currentParentId.Value);
            currentParentId = parent?.ParentId;
        }

        return false;
    }

    #endregion

    #region CRUD Methods

    public async Task<CategoryResult> CreateCategoryAsync(CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return CategoryResult.Fail(CategoryErrorType.ValidationError, "Tên danh mục không được để trống");

        var slug = string.IsNullOrWhiteSpace(request.Slug) 
            ? GenerateSlug(request.Name) 
            : request.Slug;

        if (await _unitOfWork.Categories.SlugExistsAsync(slug))
            return CategoryResult.Fail(CategoryErrorType.DuplicateSlug, $"Slug '{slug}' đã tồn tại");

        if (request.ParentId.HasValue)
        {
            var parent = await _unitOfWork.Categories.GetByIdAsync(request.ParentId.Value);
            if (parent == null || parent.IsDeleted)
                return CategoryResult.Fail(CategoryErrorType.InvalidParent, $"Danh mục cha với ID {request.ParentId} không tồn tại hoặc đã bị xóa");
        }

        var sortOrder = await _unitOfWork.Categories.GetMaxSortOrderAsync(request.ParentId) + 1;

        var category = new Category
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description,
            Image = request.Image,
            ParentId = request.ParentId,
            SortOrder = sortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(category);
    }

    public async Task<CategoryResult> UpdateCategoryAsync(int id, UpdateCategoryRequest request)
    {
        var category = await _unitOfWork.Categories.GetByIdWithChildrenAsync(id);

        if (category == null)
            return CategoryResult.Fail(CategoryErrorType.NotFound, $"Không tìm thấy danh mục với ID {id}");

        if (string.IsNullOrWhiteSpace(request.Name))
            return CategoryResult.Fail(CategoryErrorType.ValidationError, "Tên danh mục không được để trống");

        var slug = string.IsNullOrWhiteSpace(request.Slug) 
            ? GenerateSlug(request.Name) 
            : request.Slug;

        if (await _unitOfWork.Categories.SlugExistsAsync(slug, id))
            return CategoryResult.Fail(CategoryErrorType.DuplicateSlug, $"Slug '{slug}' đã tồn tại");

        if (request.ParentId != category.ParentId && request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                return CategoryResult.Fail(CategoryErrorType.CircularReference, "Không thể di chuyển danh mục vào chính nó");

            if (await IsDescendantOfAsync(request.ParentId.Value, id))
                return CategoryResult.Fail(CategoryErrorType.CircularReference, "Không thể di chuyển danh mục vào danh mục con của nó");

            var newParent = await _unitOfWork.Categories.GetByIdAsync(request.ParentId.Value);
            if (newParent == null || newParent.IsDeleted)
                return CategoryResult.Fail(CategoryErrorType.InvalidParent, $"Danh mục cha với ID {request.ParentId} không tồn tại hoặc đã bị xóa");
        }

        if (request.ParentId != category.ParentId)
            category.SortOrder = await _unitOfWork.Categories.GetMaxSortOrderAsync(request.ParentId) + 1;

        category.Name = request.Name.Trim();
        category.Slug = slug;
        category.Description = request.Description;
        category.Image = request.Image;
        category.ParentId = request.ParentId;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(category);
    }

    public async Task<CategoryResult> DeleteCategoryAsync(int id)
    {
        var category = await _unitOfWork.Categories.GetByIdWithAllAsync(id);

        if (category == null)
            return CategoryResult.Fail(CategoryErrorType.NotFound, $"Không tìm thấy danh mục với ID {id}");

        if (category.Children.Any())
            return CategoryResult.Fail(CategoryErrorType.HasChildren, $"Không thể xóa danh mục có {category.Children.Count} danh mục con");

        // Check if any products in this category are in orders
        if (category.Products.Any())
        {
            var productIds = category.Products.Select(p => p.Id).ToList();
            var hasOrderItems = await _unitOfWork.OrderItems
                .AnyAsync(oi => productIds.Contains(oi.ProductId));
            
            if (hasOrderItems)
            {
                return CategoryResult.Fail(CategoryErrorType.HasProducts, 
                    $"Không thể xóa danh mục vì có sản phẩm đã được đặt hàng. Hãy sử dụng chức năng xóa mềm.");
            }

            // Delete all products in this category (they have no orders)
            if (_unitOfWork is Repositories.UnitOfWork concrete && concrete.Context.Database.IsSqlServer())
            {
                foreach (var product in category.Products)
                {
                    product.ImagesJson = "[]";
                    product.TagsJson = "[]";
                }
            }
            else
            {
                var images = await _unitOfWork.ProductImages.Query()
                    .Where(pi => productIds.Contains(pi.ProductId))
                    .ToListAsync();
                _unitOfWork.ProductImages.RemoveRange(images);
            }

            // Delete product variants
            var variants = await _unitOfWork.ProductVariants.Query()
                .Where(pv => productIds.Contains(pv.ProductId))
                .ToListAsync();
            _unitOfWork.ProductVariants.RemoveRange(variants);

            foreach (var product in category.Products.ToList())
            {
                // Clear tags
                product.Tags.Clear();

                // Delete product
                _unitOfWork.Products.Remove(product);
            }
        }

        _unitOfWork.Categories.Remove(category);
        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(category);
    }

    #endregion

    #region Hierarchy Methods

    public async Task<List<CategoryTreeNode>> GetCategoryTreeAsync()
    {
        var categories = await _unitOfWork.Categories.GetAllWithProductsAsync();
        return BuildTree(categories, null, 0);
    }

    private List<CategoryTreeNode> BuildTree(List<Category> allCategories, int? parentId, int level)
    {
        return allCategories
            .Where(c => c.ParentId == parentId && !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryTreeNode
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                Image = c.Image,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                ProductCount = c.Products?.Count ?? 0,
                Level = level,
                ParentId = c.ParentId,
                Children = BuildTree(allCategories, c.Id, level + 1)
            })
            .ToList();
    }

    public async Task<CategoryResult> MoveCategoryAsync(int categoryId, int? newParentId)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
        if (category == null)
            return CategoryResult.Fail(CategoryErrorType.NotFound, $"Không tìm thấy danh mục với ID {categoryId}");

        if (newParentId.HasValue && newParentId.Value == categoryId)
            return CategoryResult.Fail(CategoryErrorType.CircularReference, "Không thể di chuyển danh mục vào chính nó");

        if (newParentId.HasValue && await IsDescendantOfAsync(newParentId.Value, categoryId))
            return CategoryResult.Fail(CategoryErrorType.CircularReference, "Không thể di chuyển danh mục vào danh mục con của nó");

        if (newParentId.HasValue)
        {
            var newParent = await _unitOfWork.Categories.GetByIdAsync(newParentId.Value);
            if (newParent == null || newParent.IsDeleted)
                return CategoryResult.Fail(CategoryErrorType.InvalidParent, $"Danh mục cha với ID {newParentId} không tồn tại hoặc đã bị xóa");
        }

        category.ParentId = newParentId;
        category.SortOrder = await _unitOfWork.Categories.GetMaxSortOrderAsync(newParentId) + 1;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(category);
    }

    #endregion

    #region Sorting Methods

    public async Task<CategoryResult> UpdateSortOrderAsync(int categoryId, int newSortOrder)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
        if (category == null)
            return CategoryResult.Fail(CategoryErrorType.NotFound, $"Không tìm thấy danh mục với ID {categoryId}");

        category.SortOrder = newSortOrder;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(category);
    }

    public async Task<CategoryResult> ReorderCategoriesAsync(int? parentId, List<int> categoryIds)
    {
        var categories = (await _unitOfWork.Categories
            .FindAsync(c => c.ParentId == parentId && categoryIds.Contains(c.Id)))
            .ToList();

        if (categories.Count != categoryIds.Count)
            return CategoryResult.Fail(CategoryErrorType.ValidationError, "Một số danh mục không tồn tại hoặc không cùng danh mục cha");

        for (int i = 0; i < categoryIds.Count; i++)
        {
            var category = categories.First(c => c.Id == categoryIds[i]);
            category.SortOrder = i + 1;
            category.UpdatedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(null!);
    }

    #endregion

    #region Soft Delete & Restore Methods

    public async Task<CategoryResult> SoftDeleteCategoryAsync(int id)
    {
        var category = await _unitOfWork.Categories.GetByIdWithChildrenAsync(id);

        if (category == null)
            return CategoryResult.Fail(CategoryErrorType.NotFound, $"Không tìm thấy danh mục với ID {id}");

        var activeChildCount = category.Children.Count(child => !child.IsDeleted);
        if (activeChildCount > 0)
            return CategoryResult.Fail(CategoryErrorType.HasChildren, $"Không thể chuyển danh mục có {activeChildCount} danh mục con vào thùng rác");

        category.IsDeleted = true;
        category.DeletedAt = DateTime.UtcNow;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(category);
    }

    public async Task<CategoryResult> RestoreCategoryAsync(int id)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);

        if (category == null)
            return CategoryResult.Fail(CategoryErrorType.NotFound, $"Không tìm thấy danh mục với ID {id}");

        if (!category.IsDeleted)
            return CategoryResult.Fail(CategoryErrorType.ValidationError, "Danh mục này chưa bị xóa");

        if (category.ParentId.HasValue)
        {
            var parent = await _unitOfWork.Categories.GetByIdAsync(category.ParentId.Value);
            if (parent == null || parent.IsDeleted)
                return CategoryResult.Fail(CategoryErrorType.InvalidParent, "Hãy khôi phục danh mục cha trước khi khôi phục danh mục này");
        }

        category.IsDeleted = false;
        category.DeletedAt = null;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return CategoryResult.Ok(category);
    }

    public async Task<List<Category>> GetDeletedCategoriesAsync()
    {
        return await _unitOfWork.Categories.GetDeletedCategoriesAsync();
    }

    #endregion
}
