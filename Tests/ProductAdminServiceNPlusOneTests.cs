using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Chat.Knowledge;

namespace Fruitables.Tests;

// Guardrail: tag writes are aggregate JSON writes and must not query the legacy tag table.
public class ProductAdminServiceNPlusOneTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task UpdateTagsAsync_DoesNotQueryLegacyTags(int tagCount)
    {
        var (interceptor, options) = SeedAsync(tagCount, seedExistingTag: true);
        // Đăng ký pattern trước khi chạy query để interceptor đếm từ đầu.
        interceptor.Register("ProductTags");

        // Build tag name list: first half are existing tags, second half are new tags.
        var existingNames = Enumerable.Range(1, tagCount).Select(i => $"ExistingTag{i}").ToList();
        var newNames = Enumerable.Range(1, tagCount).Select(i => $"NewTag{i}").ToList();
        var allTagNames = existingNames.Concat(newNames).ToList();

        using var context = new ApplicationDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var imageMock = new Mock<IImageUploadService>();
        var service = new ProductAdminService(
            unitOfWork,
            imageMock.Object,
            Mock.Of<IIndexingService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductAdminService>.Instance);

        var result = await service.UpdateTagsAsync(10, allTagNames);

        Assert.True(result.Success);
        Assert.Equal(0, interceptor.GetCount("ProductTags"));
    }

    private static (CountingQueryInterceptor interceptor, DbContextOptions<ApplicationDbContext> options)
        SeedAsync(int tagCount, bool seedExistingTag)
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        using var context = new ApplicationDbContext(options);

        context.Categories.Add(new Category { Id = 1, Name = "Default", Slug = "default" });
        context.Products.Add(new Product
        {
            Id = 10,
            CategoryId = 1,
            Name = "Red Apple",
            Slug = "red-apple",
            Price = 15,
            StockQuantity = 100,
            MinOrderQuantity = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        if (seedExistingTag)
        {
            for (int i = 1; i <= tagCount; i++)
            {
                context.ProductTags.Add(new ProductTag
                {
                    Id = i,
                    Name = $"ExistingTag{i}",
                    Slug = $"existing-tag-{i}"
                });
            }
        }
        context.SaveChanges();

        return (interceptor, options);
    }
}
