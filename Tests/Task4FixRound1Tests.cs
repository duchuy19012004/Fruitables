using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Catalog.Combos;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Communications;
using Fruitables.Services.Orders.Cart;
using Fruitables.Services.Orders.OrderManagement;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public sealed class Task4FixRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 8, 0, 0, TimeSpan.Zero);
    private static readonly VersionedJsonSerializer Serializer = new();

    [Fact]
    public async Task Created_price_schedule_keeps_legacy_identity_for_untouched_order_path()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Users.Add(new User
            {
                Id = 7,
                Name = "Admin",
                Email = "task4-schedule-admin@example.com",
                Password = "hashed",
                Role = UserRole.Admin,
                IsActive = true
            });
            seed.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
            seed.Products.Add(new Product
            {
                Id = 1,
                CategoryId = 1,
                Name = "Tao",
                Slug = "tao-schedule",
                Price = 100,
                StockQuantity = 10,
                IsActive = true
            });
            seed.Promotions.Add(new Promotion
            {
                Type = "coupon",
                Code = "coupon:seed",
                PayloadJson = Serializer.Serialize(new CouponPayload
                {
                    Code = "SEED",
                    Type = CouponType.Fixed,
                    Value = 1,
                    MinQuantity = 1,
                    IsActive = true
                })
            });
            await seed.SaveChangesAsync();
        }

        await using var context = new ApplicationDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var management = new PriceManagementService(
            unitOfWork,
            new FixedTimeProvider(Now),
            dbContext: context,
            serializer: Serializer);

        var created = await management.CreateScheduleAsync(new SavePriceScheduleRequest
        {
            ProductId = 1,
            DiscountType = DiscountType.FixedPrice,
            Value = 80,
            StartsAt = Now.AddMinutes(-5),
            EndsAt = Now.AddMinutes(5)
        }, 7);

        Assert.True(created.Success, created.Error);
        context.ChangeTracker.Clear();

        var promotion = await context.Promotions.SingleAsync(item => item.Type == "price-schedule");
        var legacySchedule = await context.PriceSchedules.SingleAsync();
        Assert.NotEqual(promotion.Id, legacySchedule.Id);

        var pricing = new ProductPricingService(unitOfWork, new FixedTimeProvider(Now), context, Serializer);
        var quote = await pricing.GetQuoteAsync(1);
        Assert.Equal(legacySchedule.Id, quote?.ScheduleId);

        var cart = new CartViewModel
        {
            Subtotal = 80,
            Total = 80,
            Items =
            [
                new CartItemViewModel
                {
                    ProductId = 1,
                    ProductName = "Tao",
                    Price = 80,
                    Quantity = 1,
                    IsAvailable = true
                }
            ]
        };
        var cartService = new Mock<ICartService>();
        cartService.Setup(service => service.RepriceForCheckoutAsync("schedule-order"))
            .ReturnsAsync(cart);
        cartService.Setup(service => service.ClearCartAsync("schedule-order"))
            .Returns(Task.CompletedTask);

        var order = await new OrderService(
            unitOfWork,
            cartService.Object,
            Mock.Of<IRealtimeNotifier>(),
            pricing,
            NullLogger<OrderService>.Instance)
            .CreateOrderAsync(
                new CheckoutViewModel
                {
                    FirstName = "Buyer",
                    PaymentMethod = PaymentMethod.COD,
                    ShippingMethod = ShippingMethod.Free
                },
                "schedule-order");

        var orderItem = Assert.Single(order.Items);
        Assert.Equal(legacySchedule.Id, orderItem.PriceScheduleId);
        Assert.NotNull(await context.PriceSchedules.FindAsync(orderItem.PriceScheduleId));
    }

    [Fact]
    public async Task Temporary_promotion_code_stays_within_configured_sql_length()
    {
        var interceptor = new PromotionCodeLengthInterceptor();
        var options = CreateSqliteOptionsWith(interceptor);
        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User
        {
            Id = 7,
            Name = "Admin",
            Email = "task4-length-admin@example.com",
            Password = "hashed",
            Role = UserRole.Admin,
            IsActive = true
        });
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
        context.Products.Add(new Product
        {
            Id = 1,
            CategoryId = 1,
            Name = "Tao",
            Slug = "tao-code-length",
            Price = 100,
            StockQuantity = 10,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var maxLength = context.Model.FindEntityType(typeof(Promotion))!
            .FindProperty(nameof(Promotion.Code))!
            .GetMaxLength();
        Assert.Equal(50, maxLength);

        var result = await new PriceManagementService(
            new UnitOfWork(context),
            new FixedTimeProvider(Now),
            dbContext: context,
            serializer: Serializer)
            .CreateScheduleAsync(new SavePriceScheduleRequest
            {
                ProductId = 1,
                DiscountType = DiscountType.Percentage,
                Value = 10,
                StartsAt = Now.AddHours(1)
            }, 7);

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(interceptor.PromotionCodes);
        Assert.All(interceptor.PromotionCodes, code => Assert.InRange(code.Length, 0, maxLength!.Value));
    }

    [Fact]
    public async Task Combo_create_rolls_back_promotion_when_legacy_mirror_fails()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);
        SeedComboProducts(context);
        context.Combos.Add(new Combo { Id = 90, Name = "Existing", Slug = "duplicate-combo" });
        await context.SaveChangesAsync();

        var service = new ComboService(
            new UnitOfWork(context),
            CreatePricing().Object,
            new FixedTimeProvider(Now),
            dbContext: context,
            serializer: Serializer);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(new ComboFormViewModel
        {
            Name = "Duplicate Combo",
            Slug = "duplicate-combo",
            Status = ComboLifecycleStatus.Active,
            PricingType = ComboPricingType.SumOfItems,
            Items =
            [
                new ComboItemFormModel { ProductId = 1, Quantity = 1, SortOrder = 0 },
                new ComboItemFormModel { ProductId = 2, Quantity = 1, SortOrder = 1 }
            ]
        }, 7));

        context.ChangeTracker.Clear();
        Assert.Empty(await context.Promotions.Where(item => item.Type == "combo").ToListAsync());
        Assert.Equal("duplicate-combo", (await context.Combos.SingleAsync(item => item.Id == 90)).Slug);
    }

    [Fact]
    public async Task Promotion_revision_rejects_a_second_context_update()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Promotions.Add(new Promotion
            {
                Type = "combo",
                Code = "combo:concurrency",
                PayloadJson = Serializer.Serialize(new ComboPayload
                {
                    Name = "Concurrent",
                    Slug = "concurrent",
                    Revision = 1,
                    Items = []
                }),
                Revision = 1
            });
            await seed.SaveChangesAsync();
        }

        await using var first = new ApplicationDbContext(options);
        await using var second = new ApplicationDbContext(options);
        var firstPromotion = await first.Promotions.SingleAsync();
        var secondPromotion = await second.Promotions.SingleAsync();
        firstPromotion.Revision++;
        secondPromotion.Revision++;

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Product_asset_revision_rejects_a_second_context_json_update()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
            seed.Products.Add(new Product
            {
                Id = 1,
                CategoryId = 1,
                Name = "Tao",
                Slug = "tao-asset-concurrency",
                Price = 100,
                ImagesJson = Serializer.Serialize(new ProductImagesDocument()),
                TagsJson = Serializer.Serialize(new ProductTagsDocument())
            });
            await seed.SaveChangesAsync();
        }

        await using var first = new ApplicationDbContext(options);
        await using var second = new ApplicationDbContext(options);
        var firstProduct = await first.Products.SingleAsync();
        var secondProduct = await second.Products.SingleAsync();
        var firstRevision = (int)first.Entry(firstProduct).Property("AssetRevision").CurrentValue!;
        var secondRevision = (int)second.Entry(secondProduct).Property("AssetRevision").CurrentValue!;

        firstProduct.ImagesJson = Serializer.Serialize(new ProductImagesDocument
        {
            Images = [new ProductImageDocument { Url = "/uploads/first.webp", StorageKey = "catalog/first.webp", IsPrimary = true }]
        });
        secondProduct.TagsJson = Serializer.Serialize(new ProductTagsDocument
        {
            Tags = [new ProductTagDocument { Name = "Fresh", Slug = "fresh" }]
        });
        first.Entry(firstProduct).Property("AssetRevision").CurrentValue = firstRevision + 1;
        second.Entry(secondProduct).Property("AssetRevision").CurrentValue = secondRevision + 1;

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Product_mutations_write_generic_audit_logs()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
        await context.SaveChangesAsync();

        var service = new ProductAdminService(
            new UnitOfWork(context),
            Mock.Of<IImageUploadService>(),
            Mock.Of<IIndexingService>(),
            NullLogger<ProductAdminService>.Instance,
            dbContext: context,
            serializer: Serializer);

        var created = await service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Tao audit",
            Slug = "tao-audit",
            CategoryId = 1,
            Price = 100,
            StockQuantity = 10,
            MinOrderQuantity = 1,
            IsActive = true
        });
        Assert.True(created.Success, created.ErrorMessage);

        var updated = await service.UpdateProductAsync(new UpdateProductRequest
        {
            Id = created.Product!.Id,
            Name = "Tao audit updated",
            Slug = "tao-audit",
            CategoryId = 1,
            StockQuantity = 9,
            MinOrderQuantity = 1,
            Unit = "kg",
            IsActive = true
        });
        Assert.True(updated.Success, updated.ErrorMessage);

        var tags = await service.UpdateTagsAsync(created.Product.Id, ["Fresh"]);
        Assert.True(tags.Success, tags.ErrorMessage);

        var actions = await context.AuditLogs
            .Where(log => log.EntityType == "Product" && log.EntityId == created.Product.Id)
            .Select(log => log.Action)
            .ToListAsync();
        Assert.Contains("Create", actions);
        Assert.Contains("Update", actions);
        Assert.Contains("TagUpdate", actions);
    }

    [Fact]
    public async Task Promotion_customer_code_has_a_unique_relational_constraint()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Promotion))!;
        var property = entity.FindProperty("CustomerCode");
        Assert.NotNull(property);
        Assert.Equal(50, property!.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Count == 1 && index.Properties[0].Name == "CustomerCode");

        var first = new Promotion
        {
            Type = "coupon",
            Code = "coupon:first",
            PayloadJson = CouponPayloadJson("SAVE10")
        };
        var second = new Promotion
        {
            Type = "coupon",
            Code = "coupon:second",
            PayloadJson = CouponPayloadJson("SAVE10")
        };
        context.Promotions.AddRange(first, second);
        context.Entry(first).Property("CustomerCode").CurrentValue = "SAVE10";
        context.Entry(second).Property("CustomerCode").CurrentValue = "SAVE10";

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Coupon_promotion_revision_rejects_a_second_context_update()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using (var seed = new ApplicationDbContext(options))
        {
            var promotion = new Promotion
            {
                Type = "coupon",
                Code = "coupon:revision",
                PayloadJson = CouponPayloadJson("REVISION"),
                Revision = 1
            };
            seed.Promotions.Add(promotion);
            seed.Entry(promotion).Property("CustomerCode").CurrentValue = "REVISION";
            await seed.SaveChangesAsync();
        }

        await using var first = new ApplicationDbContext(options);
        await using var second = new ApplicationDbContext(options);
        var firstPromotion = await first.Promotions.SingleAsync();
        var secondPromotion = await second.Promotions.SingleAsync();
        firstPromotion.Revision++;
        secondPromotion.Revision++;

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Add_combo_to_cart_rejects_dangling_payload_items_without_throwing()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var context = new ApplicationDbContext(options);
        context.Promotions.Add(new Promotion
        {
            Id = 1,
            Type = "combo",
            Code = "combo:1",
            PayloadJson = Serializer.Serialize(new ComboPayload
            {
                Name = "Dangling",
                Slug = "dangling",
                Items =
                [
                    new ComboItemPayload { ProductId = 999, Quantity = 1, SortOrder = 0 },
                    new ComboItemPayload { ProductId = 998, Quantity = 1, SortOrder = 1 }
                ]
            })
        });
        await context.SaveChangesAsync();

        var result = await new ComboService(
            new UnitOfWork(context),
            CreatePricing().Object,
            new FixedTimeProvider(Now),
            dbContext: context,
            serializer: Serializer)
            .AddComboToCartAsync("dangling", 1, Mock.Of<ICartService>());

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    private static string CouponPayloadJson(string code) => Serializer.Serialize(new CouponPayload
    {
        Code = code,
        Type = CouponType.Fixed,
        Value = 10,
        MinQuantity = 1,
        IsActive = true
    });

    private static void SeedComboProducts(ApplicationDbContext context)
    {
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
        context.Products.AddRange(
            new Product { Id = 1, CategoryId = 1, Name = "Tao", Slug = "tao-combo", Price = 100, StockQuantity = 10 },
            new Product { Id = 2, CategoryId = 1, Name = "Cam", Slug = "cam-combo", Price = 100, StockQuantity = 10 });
    }

    private static Mock<IProductPricingService> CreatePricing()
    {
        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(service => service.GetQuotesAsync(
                It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) => targets
                .Distinct()
                .ToDictionary(target => target, target => new PriceQuote(
                    target.ProductId,
                    target.ProductVariantId,
                    100,
                    100,
                    null)));
        return pricing;
    }

    private static DbContextOptions<ApplicationDbContext> CreateSqliteOptionsWith(
        PromotionCodeLengthInterceptor interceptor)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return options;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PromotionCodeLengthInterceptor : SaveChangesInterceptor
    {
        public List<string> PromotionCodes { get; } = [];

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void Capture(DbContext? context)
        {
            if (context == null)
                return;

            PromotionCodes.AddRange(context.ChangeTracker.Entries<Promotion>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry => entry.Entity.Code ?? string.Empty));
        }
    }
}
