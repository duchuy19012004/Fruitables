using System.ComponentModel.DataAnnotations;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Fruitables.Services.Catalog.Categories;

namespace Fruitables.Tests;

public class CategoryServiceHierarchyTests
{
    [Fact]
    public async Task CreateCategoryAsync_RejectsDeletedParent()
    {
        await using var context = CreateContext();
        context.Categories.Add(Category(1, "Deleted", "deleted", isDeleted: true));
        await context.SaveChangesAsync();
        var service = new CategoryService(context);

        var result = await service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "Child",
            ParentId = 1
        });

        Assert.False(result.Success);
        Assert.Equal(CategoryErrorType.InvalidParent, result.ErrorType);
    }

    [Fact]
    public async Task UpdateCategoryAsync_WhenParentChanges_AppendsToNewParent()
    {
        await using var context = CreateContext();
        context.Categories.AddRange(
            Category(1, "Root A", "root-a"),
            Category(2, "Root B", "root-b"),
            Category(3, "Existing child", "existing-child", parentId: 2, sortOrder: 4),
            Category(4, "Moving child", "moving-child", parentId: 1, sortOrder: 1));
        await context.SaveChangesAsync();
        var service = new CategoryService(context);

        var result = await service.UpdateCategoryAsync(4, new UpdateCategoryRequest
        {
            Name = "Moving child",
            Slug = "moving-child",
            ParentId = 2,
            IsActive = true
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.Category!.ParentId);
        Assert.Equal(5, result.Category.SortOrder);
    }

    [Fact]
    public async Task SoftDeleteCategoryAsync_RejectsCategoryWithActiveChildren()
    {
        await using var context = CreateContext();
        context.Categories.AddRange(
            Category(1, "Parent", "parent"),
            Category(2, "Child", "child", parentId: 1));
        await context.SaveChangesAsync();
        var service = new CategoryService(context);

        var result = await service.SoftDeleteCategoryAsync(1);

        Assert.False(result.Success);
        Assert.Equal(CategoryErrorType.HasChildren, result.ErrorType);
        Assert.False((await context.Categories.FindAsync(1))!.IsDeleted);
    }

    [Fact]
    public async Task RestoreCategoryAsync_RejectsCategoryWhoseParentIsDeleted()
    {
        await using var context = CreateContext();
        context.Categories.AddRange(
            Category(1, "Parent", "parent", isDeleted: true),
            Category(2, "Child", "child", parentId: 1, isDeleted: true));
        await context.SaveChangesAsync();
        var service = new CategoryService(context);

        var result = await service.RestoreCategoryAsync(2);

        Assert.False(result.Success);
        Assert.Equal(CategoryErrorType.InvalidParent, result.ErrorType);
        Assert.True((await context.Categories.FindAsync(2))!.IsDeleted);
    }

    [Fact]
    public async Task MoveCategoryAsync_RejectsMovingParentUnderItsDescendant()
    {
        await using var context = CreateContext();
        context.Categories.AddRange(
            Category(1, "Parent", "parent"),
            Category(2, "Child", "child", parentId: 1));
        await context.SaveChangesAsync();
        var service = new CategoryService(context);

        var result = await service.MoveCategoryAsync(1, 2);

        Assert.False(result.Success);
        Assert.Equal(CategoryErrorType.CircularReference, result.ErrorType);
        Assert.Null((await context.Categories.FindAsync(1))!.ParentId);
    }

    [Fact]
    public async Task ReorderCategoriesAsync_UpdatesSiblingOrder()
    {
        await using var context = CreateContext();
        context.Categories.AddRange(
            Category(1, "First", "first", sortOrder: 1),
            Category(2, "Second", "second", sortOrder: 2));
        await context.SaveChangesAsync();
        var service = new CategoryService(context);

        var result = await service.ReorderCategoriesAsync(null, new List<int> { 2, 1 });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, (await context.Categories.FindAsync(1))!.SortOrder);
        Assert.Equal(1, (await context.Categories.FindAsync(2))!.SortOrder);
    }

    [Theory]
    [InlineData(typeof(CreateCategoryViewModel), nameof(CreateCategoryViewModel.Name), 101)]
    [InlineData(typeof(CreateCategoryViewModel), nameof(CreateCategoryViewModel.Slug), 101)]
    [InlineData(typeof(EditCategoryViewModel), nameof(EditCategoryViewModel.Name), 101)]
    [InlineData(typeof(EditCategoryViewModel), nameof(EditCategoryViewModel.Slug), 101)]
    public void CategoryViewModel_RejectsValuesLongerThanDatabaseColumn(
        Type modelType,
        string propertyName,
        int length)
    {
        var model = Activator.CreateInstance(modelType)!;
        modelType.GetProperty(propertyName)!.SetValue(model, new string('a', length));
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(propertyName));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Category Category(
        int id,
        string name,
        string slug,
        int? parentId = null,
        int sortOrder = 0,
        bool isDeleted = false)
    {
        return new Category
        {
            Id = id,
            Name = name,
            Slug = slug,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
    }
}
