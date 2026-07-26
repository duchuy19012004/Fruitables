using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fruitables.Tests
{
    public class ProductAdminServiceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task UpdateTagsAsync_RemovesOldTags_And_AddsNewAndExistingTags()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options);
            
            // Seed tags
            var tag1 = new ProductTag { Id = 1, Name = "Fruit", Slug = "fruit" };
            var tag2 = new ProductTag { Id = 2, Name = "Apple", Slug = "apple" };
            context.ProductTags.AddRange(tag1, tag2);

            // Seed product with existing tags
            var product = new Product
            {
                Id = 10,
                Name = "Red Apple",
                Slug = "red-apple",
                Price = 15,
                StockQuantity = 100,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Tags = new List<ProductTag> { tag1, tag2 } // Starts with Fruit, Apple
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var unitOfWork = new UnitOfWork(context);
            var imageMock = new Mock<IImageUploadService>();
            var service = new ProductAdminService(
                unitOfWork,
                imageMock.Object,
                Mock.Of<IIndexingService>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductAdminService>.Instance);

            // Act: Update tags to ["Fruit", "Fresh"]
            // "Fruit" is an existing tag (loaded in batch)
            // "Fresh" is a brand new tag (created)
            // "Apple" should be removed from product.Tags
            var result = await service.UpdateTagsAsync(10, new List<string> { "Fruit", "Fresh" });

            // Assert
            Assert.True(result.Success);
            
            // Reload product from db with tags
            var updatedProduct = await context.Products
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == 10);
            
            Assert.NotNull(updatedProduct);
            Assert.Equal(2, updatedProduct.Tags.Count);
            
            var tagNames = updatedProduct.Tags.Select(t => t.Name).ToList();
            Assert.Contains("Fruit", tagNames);
            Assert.Contains("Fresh", tagNames);
            Assert.DoesNotContain("Apple", tagNames);

            // Ensure "Fresh" was created in ProductTags table
            var newTagInDb = await context.ProductTags.FirstOrDefaultAsync(t => t.Name == "Fresh");
            Assert.NotNull(newTagInDb);
            Assert.Equal("fresh", newTagInDb.Slug);
        }

        private static ApplicationDbContext CreateContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static ProductAdminService CreateService(ApplicationDbContext context) =>
            new(new UnitOfWork(context), Mock.Of<IImageUploadService>(), Mock.Of<IIndexingService>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductAdminService>.Instance);

        [Fact]
        public async Task UpdateVariant_last_active_variant_with_future_schedule_is_rejected()
        {
            await using var context = CreateContext();
            context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 50_000 });
            context.ProductVariants.Add(new ProductVariant
            {
                Id = 2, ProductId = 1, SKU = "TAO-1", Name = "Hộp 1kg",
                Price = 100_000, StockQuantity = 8, IsActive = true
            });
            context.PriceSchedules.Add(new PriceSchedule
            {
                ProductId = 1, ProductVariantId = 2,
                DiscountType = DiscountType.Percentage, Value = 10,
                StartsAt = DateTimeOffset.UtcNow.AddHours(1)
            });
            await context.SaveChangesAsync();

            var result = await CreateService(context).UpdateVariantAsync(2, new UpdateVariantRequest
            {
                SKU = "TAO-1", Name = "Hộp 1kg", StockQuantity = 8, IsActive = false
            });

            Assert.False(result.Success);
            Assert.Contains("lịch giá biến thể", result.ErrorMessage);
            Assert.True(context.ProductVariants.Find(2)!.IsActive);
        }

        [Fact]
        public async Task UpdateVariant_last_active_variant_without_future_schedule_copies_price_and_stock_to_product()
        {
            await using var context = CreateContext();
            context.Products.Add(new Product
            {
                Id = 1, Name = "Táo", Slug = "tao", Price = 50_000,
                StockQuantity = 1, PriceRevision = 3
            });
            context.ProductVariants.Add(new ProductVariant
            {
                Id = 2, ProductId = 1, SKU = "TAO-1", Name = "Hộp 1kg",
                Price = 100_000, StockQuantity = 8, IsActive = true
            });
            await context.SaveChangesAsync();

            var result = await CreateService(context).UpdateVariantAsync(2, new UpdateVariantRequest
            {
                SKU = "TAO-1", Name = "Hộp 1kg", StockQuantity = 8, IsActive = false
            });

            var product = context.Products.Find(1)!;
            Assert.True(result.Success);
            Assert.False(context.ProductVariants.Find(2)!.IsActive);
            Assert.Equal(100_000, product.Price);
            Assert.Equal(8, product.StockQuantity);
            Assert.Equal(4, product.PriceRevision);
        }
    }
}
