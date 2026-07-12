using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
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
    }
}
