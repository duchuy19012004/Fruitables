using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.Services.Catalog.Categories;

namespace Fruitables.Tests;

// Guardrail N+1: DeleteCategoryAsync phải lookup ProductImages và ProductVariants theo
// batch (1 SELECT mỗi bảng cho mọi số product trong category), không query từng product.
// Test với 1, 5, 20 product.
public class CategoryServiceNPlusOneTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task DeleteCategoryAsync_ImageAndVariantLookupsAreBatched(int productCount)
    {
        var (interceptor, options, categoryId) = SeedCategoryWithProducts(productCount);
        interceptor.Register("ProductImages");
        interceptor.Register("ProductVariants");

        using var context = new ApplicationDbContext(options);
        var service = new CategoryService(context);

        var result = await service.DeleteCategoryAsync(categoryId);

        Assert.True(result.Success, result.ErrorMessage);
        // 1 SELECT ProductImages + 1 SELECT ProductVariants cho batch lookup,
        // không phụ thuộc productCount.
        Assert.Equal(1, interceptor.GetCount("ProductImages"));
        Assert.Equal(1, interceptor.GetCount("ProductVariants"));
    }

    private static (CountingQueryInterceptor interceptor, DbContextOptions<ApplicationDbContext> options, int categoryId)
        SeedCategoryWithProducts(int productCount)
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        using var context = new ApplicationDbContext(options);

        var category = new Category
        {
            Id = 1,
            Name = "Fruits",
            Slug = "fruits",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Categories.Add(category);

        for (int i = 1; i <= productCount; i++)
        {
            var product = new Product
            {
                Id = i,
                CategoryId = 1,
                Name = $"P{i}",
                Slug = $"p{i}",
                Price = 10,
                StockQuantity = 100,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Products.Add(product);

            // 2 images + 2 variants per product to make the delete non-trivial.
            context.ProductImages.Add(new ProductImage
            {
                ProductId = i,
                ImageUrl = $"/img/{i}-1.jpg",
                IsPrimary = true,
                SortOrder = 0
            });
            context.ProductImages.Add(new ProductImage
            {
                ProductId = i,
                ImageUrl = $"/img/{i}-2.jpg",
                IsPrimary = false,
                SortOrder = 1
            });
            context.ProductVariants.Add(new ProductVariant
            {
                ProductId = i,
                SKU = $"SKU-{i}-A",
                Name = $"V{i}A",
                Price = 10,
                StockQuantity = 5,
                IsActive = true
            });
            context.ProductVariants.Add(new ProductVariant
            {
                ProductId = i,
                SKU = $"SKU-{i}-B",
                Name = $"V{i}B",
                Price = 12,
                StockQuantity = 5,
                IsActive = true
            });
        }
        context.SaveChanges();

        return (interceptor, options, category.Id);
    }
}
