using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Catalog.Combos;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Communications;
using Fruitables.Services.Pricing.Coupons;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public sealed class CatalogPromotionJsonTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 8, 0, 0, TimeSpan.Zero);
    private readonly IJsonDocumentSerializer _serializer = new VersionedJsonSerializer();

    [Fact]
    public async Task Product_reads_images_and_tags_from_typed_json_in_primary_order()
    {
        await using var context = CreateContext();
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
        context.Products.Add(new Product
        {
            Id = 1,
            CategoryId = 1,
            Name = "Tao JSON",
            Slug = "tao-json",
            Price = 100,
            ImagesJson = _serializer.Serialize(new ProductImagesDocument
            {
                Images =
                [
                    new ProductImageDocument { Url = "/uploads/products/second.webp", StorageKey = "uploads/products/second.webp", SortOrder = 1 },
                    new ProductImageDocument { Url = "/uploads/products/primary.webp", StorageKey = "uploads/products/primary.webp", IsPrimary = true, SortOrder = 0 }
                ]
            }),
            TagsJson = _serializer.Serialize(new ProductTagsDocument
            {
                Tags = [new ProductTagDocument { Name = "Fresh", Slug = "fresh" }]
            })
        });
        context.ProductImages.Add(new ProductImage
        {
            Id = 99,
            ProductId = 1,
            ImageUrl = "/uploads/products/legacy.webp",
            IsPrimary = true
        });
        context.ProductTags.Add(new ProductTag { Id = 99, Name = "Legacy", Slug = "legacy" });
        await context.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(context);
        var service = new ProductService(
            unitOfWork,
            new FixedTimeProvider(Now),
            new ProductPricingService(unitOfWork, new FixedTimeProvider(Now)));

        var product = await service.GetProductByIdAsync(1);

        Assert.NotNull(product);
        Assert.Collection(product!.Images,
            image =>
            {
                Assert.Equal("/uploads/products/primary.webp", image.ImageUrl);
                Assert.True(image.IsPrimary);
            },
            image => Assert.Equal("/uploads/products/second.webp", image.ImageUrl));
        Assert.Equal("Fresh", Assert.Single(product.Tags).Name);
    }

    [Fact]
    public async Task Product_asset_writes_update_json_without_writing_legacy_tables()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 1,
            Name = "Tao",
            Slug = "tao",
            Price = 100,
            ImagesJson = _serializer.Serialize(new ProductImagesDocument()),
            TagsJson = _serializer.Serialize(new ProductTagsDocument())
        });
        await context.SaveChangesAsync();

        var imageService = new Mock<IImageUploadService>();
        imageService.Setup(service => service.IsValidImageFile(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>())).Returns(true);
        imageService.Setup(service => service.IsValidFileSize(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<long>())).Returns(true);
        imageService.Setup(service => service.UploadProductImageAsync(
                It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/products/new.webp");

        var service = new ProductAdminService(
            new UnitOfWork(context),
            imageService.Object,
            Mock.Of<Fruitables.Services.Chat.Knowledge.IIndexingService>(),
            NullLogger<ProductAdminService>.Instance,
            dbContext: context,
            serializer: _serializer);

        var imageResult = await service.AddImagesAsync(1, [Mock.Of<Microsoft.AspNetCore.Http.IFormFile>()]);
        var tagResult = await service.UpdateTagsAsync(1, ["Fresh", "Organic"]);
        var product = await context.Products.SingleAsync(product => product.Id == 1);
        var images = _serializer.Deserialize<ProductImagesDocument>(product.ImagesJson);
        var tags = _serializer.Deserialize<ProductTagsDocument>(product.TagsJson);

        Assert.True(imageResult.Success);
        Assert.True(tagResult.Success);
        Assert.Contains(images.Images, image => image.Url == "/uploads/products/new.webp");
        Assert.Equal(["Fresh", "Organic"], tags.Tags.Select(tag => tag.Name).ToArray());
        Assert.Empty(await context.ProductImages.ToListAsync());
        Assert.Empty(await context.ProductTags.ToListAsync());
    }

    [Fact]
    public async Task Combo_reads_items_from_typed_payload_and_resolves_product_variants()
    {
        await using var context = CreateContext();
        var product = new Product { Id = 1, Name = "Tao", Slug = "tao", Price = 100, StockQuantity = 5 };
        product.Variants.Add(new ProductVariant
        {
            Id = 2,
            ProductId = 1,
            Name = "Hop 1kg",
            SKU = "TAO-1",
            Price = 100,
            StockQuantity = 5,
            IsActive = true
        });
        context.Products.Add(product);
        context.Products.Add(new Product { Id = 3, Name = "Cam", Slug = "cam", Price = 80, StockQuantity = 5 });
        context.Promotions.Add(new Promotion
        {
            Id = 10,
            Type = "combo",
            Code = "combo:10",
            PayloadJson = _serializer.Serialize(new ComboPayload
            {
                Name = "Tao Cam",
                Slug = "tao-cam",
                IsActive = true,
                Status = ComboLifecycleStatus.Active,
                PricingType = ComboPricingType.SumOfItems,
                Revision = 4,
                Items =
                [
                    new ComboItemPayload { ProductId = 1, ProductVariantId = 2, Quantity = 1, SortOrder = 0 },
                    new ComboItemPayload { ProductId = 3, Quantity = 2, SortOrder = 1 }
                ]
            }),
            IsActive = true,
            Revision = 4
        });
        await context.SaveChangesAsync();

        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(service => service.GetQuotesAsync(It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) => targets
                .Distinct()
                .ToDictionary(target => target, target => new PriceQuote(target.ProductId, target.ProductVariantId, 100, 100, null)));

        var cards = await new ComboService(
            new UnitOfWork(context),
            pricing.Object,
            new FixedTimeProvider(Now),
            dbContext: context,
            serializer: _serializer)
            .GetActiveComboCardsAsync();

        var card = Assert.Single(cards);
        Assert.Equal(10, card.Id);
        Assert.Collection(card.Items,
            item =>
            {
                Assert.Equal(1, item.ProductId);
                Assert.Equal(2, item.ProductVariantId);
                Assert.Equal("Hop 1kg", item.VariantName);
            },
            item => Assert.Equal(3, item.ProductId));
    }

    [Fact]
    public async Task Coupon_eligibility_reads_typed_payload()
    {
        await using var context = CreateContext();
        context.Promotions.Add(new Promotion
        {
            Id = 20,
            Type = "coupon",
            Code = "coupon:20",
            PayloadJson = _serializer.Serialize(new CouponPayload
            {
                Code = "SAVE10",
                Type = CouponType.Percentage,
                Value = 10,
                MinOrderAmount = 100,
                MinQuantity = 2,
                UsedCount = 0,
                IsActive = true
            }),
            IsActive = true
        });
        await context.SaveChangesAsync();

        var coupons = await new CouponService(new UnitOfWork(context), context, _serializer)
            .GetAvailableCouponsAsync(200, 2);

        var coupon = Assert.Single(coupons);
        Assert.Equal(20, coupon.Id);
        Assert.Equal("SAVE10", coupon.Code);
        Assert.True(coupon.IsEligible);
        Assert.Equal(20, coupon.DiscountAmount);
    }

    [Fact]
    public async Task Pricing_selects_only_the_active_typed_price_schedule()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Tao", Slug = "tao", Price = 100 });
        context.Promotions.AddRange(
            new Promotion
            {
                Id = 30,
                Type = "price-schedule",
                Code = "price-schedule:30",
                PayloadJson = _serializer.Serialize(new PriceSchedulePayload
                {
                    ProductId = 1,
                    DiscountType = DiscountType.FixedPrice,
                    Value = 80,
                    StartsAt = Now.AddHours(-1),
                    EndsAt = Now.AddHours(1),
                    Revision = 1,
                    CreatedAt = Now.AddDays(-1),
                    UpdatedAt = Now.AddDays(-1)
                }),
                IsActive = true,
                StartsAt = Now.AddHours(-1),
                EndsAt = Now.AddHours(1)
            },
            new Promotion
            {
                Id = 31,
                Type = "coupon",
                Code = "coupon:31",
                PayloadJson = _serializer.Serialize(new CouponPayload
                {
                    Code = "NOT-A-SCHEDULE",
                    Type = CouponType.Fixed,
                    Value = 99,
                    MinQuantity = 1,
                    IsActive = true
                }),
                IsActive = true
            });
        await context.SaveChangesAsync();

        var quote = await new ProductPricingService(
            new UnitOfWork(context),
            new FixedTimeProvider(Now),
            context,
            _serializer)
            .GetQuoteAsync(1);

        Assert.NotNull(quote);
        Assert.Equal(80, quote!.EffectivePrice);
        Assert.Equal(30, quote.ScheduleId);
    }

    [Fact]
    public async Task Price_schedule_revision_conflict_is_rejected_from_promotion_payload()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Tao", Slug = "tao", Price = 100 });
        context.Promotions.Add(new Promotion
        {
            Id = 40,
            Type = "price-schedule",
            Code = "price-schedule:40",
            PayloadJson = _serializer.Serialize(new PriceSchedulePayload
            {
                ProductId = 1,
                DiscountType = DiscountType.Percentage,
                Value = 10,
                StartsAt = Now.AddHours(1),
                Revision = 3,
                CreatedAt = Now,
                UpdatedAt = Now
            }),
            IsActive = true,
            StartsAt = Now.AddHours(1),
            Revision = 3
        });
        await context.SaveChangesAsync();

        var result = await new Fruitables.Services.Pricing.ProductPricing.PriceManagementService(
            new UnitOfWork(context),
            new FixedTimeProvider(Now),
            dbContext: context,
            serializer: _serializer)
            .UpdateScheduleAsync(40, new SavePriceScheduleRequest
            {
                ProductId = 1,
                DiscountType = DiscountType.Percentage,
                Value = 20,
                StartsAt = Now.AddHours(1),
                ExpectedRevision = 2
            },
            7);

        Assert.False(result.Success);
        Assert.Contains("đã thay đổi", result.Error);
    }

    [Fact]
    public async Task Combo_audit_output_reads_generic_audit_rows()
    {
        await using var context = CreateContext();
        context.Promotions.Add(new Promotion
        {
            Id = 50,
            Type = "combo",
            Code = "combo:50",
            PayloadJson = _serializer.Serialize(new ComboPayload
            {
                Name = "Combo audit",
                Slug = "combo-audit",
                Status = ComboLifecycleStatus.Active,
                PricingType = ComboPricingType.SumOfItems,
                Revision = 2,
                Items = []
            }),
            IsActive = true,
            Revision = 2
        });
        context.AuditLogs.Add(new AuditLog
        {
            SourceType = "Promotion",
            SourceId = 50,
            Action = "Update",
            EntityType = "Combo",
            EntityId = 50,
            ChangedByAdminId = 7,
            NewValue = "{\"revision\":2}"
        });
        await context.SaveChangesAsync();

        var audit = await new ComboService(
            new UnitOfWork(context),
            Mock.Of<IProductPricingService>(),
            dbContext: context,
            serializer: _serializer)
            .GetAuditAsync(50);

        Assert.NotNull(audit);
        var row = Assert.Single(audit!.Items);
        Assert.Equal("Update", row.Action);
        Assert.Equal(2, row.Revision);
    }

    [Fact]
    public async Task Json_read_paths_do_not_query_legacy_asset_or_promotion_tables()
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
            seed.Products.Add(new Product
            {
                Id = 1,
                CategoryId = 1,
                Name = "Tao",
                Slug = "tao",
                Price = 100,
                ImagesJson = _serializer.Serialize(new ProductImagesDocument
                {
                    Images = [new ProductImageDocument { Url = "/uploads/products/tao.webp", StorageKey = "uploads/products/tao.webp", IsPrimary = true }]
                }),
                TagsJson = _serializer.Serialize(new ProductTagsDocument
                {
                    Tags = [new ProductTagDocument { Name = "Fresh", Slug = "fresh" }]
                })
            });
            seed.Products.Add(new Product
            {
                Id = 2,
                CategoryId = 1,
                Name = "Cam",
                Slug = "cam",
                Price = 80,
                StockQuantity = 5,
                ImagesJson = _serializer.Serialize(new ProductImagesDocument()),
                TagsJson = _serializer.Serialize(new ProductTagsDocument())
            });
            seed.Promotions.Add(new Promotion
            {
                Id = 60,
                Type = "coupon",
                Code = "coupon:60",
                PayloadJson = _serializer.Serialize(new CouponPayload
                {
                    Code = "SAVE",
                    Type = CouponType.Fixed,
                    Value = 5,
                    MinQuantity = 1,
                    IsActive = true
                }),
                IsActive = true
            });
            seed.Promotions.Add(new Promotion
            {
                Id = 61,
                Type = "combo",
                Code = "combo:61",
                PayloadJson = _serializer.Serialize(new ComboPayload
                {
                    Name = "Tao Cam",
                    Slug = "tao-cam",
                    IsActive = true,
                    Status = ComboLifecycleStatus.Active,
                    PricingType = ComboPricingType.SumOfItems,
                    Items =
                    [
                        new ComboItemPayload { ProductId = 1, Quantity = 1 },
                        new ComboItemPayload { ProductId = 2, Quantity = 1 }
                    ]
                }),
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        foreach (var table in new[] { "ProductImages", "ProductTags", "ComboItems", "Coupons", "PriceSchedules" })
            interceptor.Register(table);

        await using var context = new ApplicationDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var pricing = new ProductPricingService(unitOfWork, new FixedTimeProvider(Now), context, _serializer);
        var productService = new ProductService(unitOfWork, new FixedTimeProvider(Now), pricing, _serializer);
        await productService.GetProductByIdAsync(1);
        await new ProductAdminService(
            unitOfWork,
            Mock.Of<IImageUploadService>(),
            Mock.Of<Fruitables.Services.Chat.Knowledge.IIndexingService>(),
            NullLogger<ProductAdminService>.Instance,
            dbContext: context,
            serializer: _serializer)
            .GetProductByIdAsync(1);
        await new CouponService(unitOfWork, context, _serializer).GetAvailableCouponsAsync(100, 1);
        pricing.ProjectCatalogPrices(context.Products.Where(product => product.Id == 1)).ToList();
        var comboPricing = new Mock<IProductPricingService>();
        comboPricing.Setup(service => service.GetQuotesAsync(It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) => targets
                .Distinct()
                .ToDictionary(target => target, target => new PriceQuote(target.ProductId, target.ProductVariantId, 100, 100, null)));
        await new ComboService(
            unitOfWork,
            comboPricing.Object,
            dbContext: context,
            serializer: _serializer)
            .GetActiveComboCardsAsync();

        Assert.Equal(0, interceptor.GetCount("ProductImages"));
        Assert.Equal(0, interceptor.GetCount("ProductTags"));
        Assert.Equal(0, interceptor.GetCount("ComboItems"));
        Assert.Equal(0, interceptor.GetCount("Coupons"));
        Assert.Equal(0, interceptor.GetCount("PriceSchedules"));
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
