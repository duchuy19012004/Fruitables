using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Infrastructure.Json;

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
            
            var serializer = new VersionedJsonSerializer();
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
                TagsJson = serializer.Serialize(new ProductTagsDocument
                {
                    Tags =
                    [
                        new ProductTagDocument { Name = "Fruit", Slug = "fruit" },
                        new ProductTagDocument { Name = "Apple", Slug = "apple" }
                    ]
                })
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var unitOfWork = new UnitOfWork(context);
            var imageMock = new Mock<IImageUploadService>();
            var service = new ProductAdminService(
                unitOfWork,
                imageMock.Object,
                Mock.Of<IIndexingService>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductAdminService>.Instance,
                dbContext: context,
                serializer: serializer);

            var result = await service.UpdateTagsAsync(10, new List<string> { "Fruit", "Fresh" });

            // Assert
            Assert.True(result.Success);
            
            var updatedProduct = await context.Products.FirstOrDefaultAsync(p => p.Id == 10);
            
            Assert.NotNull(updatedProduct);
            var tags = serializer.Deserialize<ProductTagsDocument>(updatedProduct!.TagsJson).Tags;
            Assert.Equal(["Fruit", "Fresh"], tags.Select(tag => tag.Name).ToArray());
            Assert.Empty(await context.ProductTags.ToListAsync());
        }

        private static ApplicationDbContext CreateContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static ProductAdminService CreateService(ApplicationDbContext context) =>
            new(new UnitOfWork(context), Mock.Of<IImageUploadService>(), Mock.Of<IIndexingService>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductAdminService>.Instance,
                dbContext: context);

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
            var serializer = new VersionedJsonSerializer();
            context.Promotions.Add(new Promotion
            {
                Id = 2,
                Type = "price-schedule",
                Code = "price-schedule:2",
                PayloadJson = serializer.Serialize(new PriceSchedulePayload
                {
                    ProductId = 1,
                    ProductVariantId = 2,
                    DiscountType = DiscountType.Percentage,
                    Value = 10,
                    StartsAt = DateTimeOffset.UtcNow.AddHours(1),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }),
                IsActive = true,
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
