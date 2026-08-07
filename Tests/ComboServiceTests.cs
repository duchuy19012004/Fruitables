using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Fruitables.Services.Pricing.Combos;
using Fruitables.Services.Pricing.Coupons;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.Services.Catalog.Combos;
using Fruitables.Services.Orders.Cart;
using Fruitables.Services.Infrastructure.Json;

namespace Fruitables.Tests;

public class ComboServiceTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static Mock<IProductPricingService> CreatePricing()
    {
        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(service => service.GetQuotesAsync(
                It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) =>
                targets.Distinct().ToDictionary(
                    target => target,
                    target => new PriceQuote(target.ProductId, target.ProductVariantId, 100_000m, 100_000m, null)));
        return pricing;
    }

    private static Promotion ComboPromotion(
        int id,
        string name,
        string slug,
        params ComboItemPayload[] items) => new()
        {
            Id = id,
            Type = "combo",
            Code = $"combo:{id}",
            PayloadJson = new VersionedJsonSerializer().Serialize(new ComboPayload
            {
                Name = name,
                Slug = slug,
                IsActive = true,
                Status = ComboLifecycleStatus.Active,
                PricingType = ComboPricingType.SumOfItems,
                Revision = 1,
                Items = items.ToList()
            }),
            IsActive = true,
            Revision = 1
        };

    private static Product Product(int id, string name, int stock = 10) => new()
    {
        Id = id,
        Name = name,
        Slug = $"product-{id}",
        Price = 100_000m,
        StockQuantity = stock,
        MinOrderQuantity = 1,
        IsActive = true
    };

    [Fact]
    public async Task CreateAsync_rejects_combo_with_fewer_than_two_items()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        var service = new ComboService(new UnitOfWork(context), CreatePricing().Object,
            dbContext: context, serializer: new VersionedJsonSerializer());

        var result = await service.CreateAsync(new ComboFormViewModel
        {
            Name = "Combo rỗng",
            Items = []
        });

        Assert.False(result.Success);
        Assert.Contains("ít nhất 2", result.ErrorMessage);
        Assert.Empty(context.Combos);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_product_variant_lines()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        var service = new ComboService(new UnitOfWork(context), CreatePricing().Object,
            dbContext: context, serializer: new VersionedJsonSerializer());

        var result = await service.CreateAsync(new ComboFormViewModel
        {
            Name = "Combo trùng",
            Items =
            [
                new ComboItemFormModel { ProductId = 1, Quantity = 1 },
                new ComboItemFormModel { ProductId = 1, Quantity = 2 }
            ]
        });

        Assert.False(result.Success);
        Assert.Contains("trùng", result.ErrorMessage);
        Assert.Empty(context.Combos);
    }

    [Fact]
    public async Task CreateAsync_rejects_variant_that_does_not_belong_to_product()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        var apple = Product(1, "Táo");
        var orange = Product(2, "Cam");
        orange.Variants.Add(new ProductVariant
        {
            Id = 20,
            ProductId = orange.Id,
            Name = "Túi 2kg",
            SKU = "CAM-2KG",
            Price = 180_000m,
            StockQuantity = 5,
            IsActive = true
        });
        context.Products.AddRange(apple, orange);
        await context.SaveChangesAsync();
        var service = new ComboService(new UnitOfWork(context), CreatePricing().Object,
            dbContext: context, serializer: new VersionedJsonSerializer());

        var result = await service.CreateAsync(new ComboFormViewModel
        {
            Name = "Combo sai biến thể",
            Items =
            [
                new ComboItemFormModel { ProductId = apple.Id, ProductVariantId = 20, Quantity = 1 },
                new ComboItemFormModel { ProductId = orange.Id, ProductVariantId = 20, Quantity = 1 }
            ]
        });

        Assert.False(result.Success);
        Assert.Contains("không thuộc", result.ErrorMessage);
        Assert.Empty(context.Combos);
    }

    [Fact]
    public async Task CreateAsync_rejects_active_combo_when_an_item_has_insufficient_stock()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        context.Products.AddRange(Product(1, "Táo", 10), Product(2, "Cam", 0));
        await context.SaveChangesAsync();
        var service = new ComboService(new UnitOfWork(context), CreatePricing().Object,
            dbContext: context, serializer: new VersionedJsonSerializer());

        var result = await service.CreateAsync(new ComboFormViewModel
        {
            Name = "Combo hết hàng",
            IsActive = true,
            Items =
            [
                new ComboItemFormModel { ProductId = 1, Quantity = 1 },
                new ComboItemFormModel { ProductId = 2, Quantity = 1 }
            ]
        });

        Assert.False(result.Success);
        Assert.Contains("không đủ tồn kho", result.ErrorMessage);
        Assert.Empty(context.Combos);
    }

    [Fact]
    public async Task CreateAsync_rejects_fixed_combo_price_above_current_item_total()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        context.Products.AddRange(Product(1, "Táo"), Product(2, "Cam"));
        await context.SaveChangesAsync();
        var service = new ComboService(new UnitOfWork(context), CreatePricing().Object,
            dbContext: context, serializer: new VersionedJsonSerializer());

        var result = await service.CreateAsync(new ComboFormViewModel
        {
            Name = "Combo sai giá",
            PricingType = ComboPricingType.FixedPrice,
            FixedPrice = 250_000m,
            Items =
            [
                new ComboItemFormModel { ProductId = 1, Quantity = 1 },
                new ComboItemFormModel { ProductId = 2, Quantity = 1 }
            ]
        });

        Assert.False(result.Success);
        Assert.Contains("không vượt quá", result.ErrorMessage);
        Assert.Empty(context.Combos);
    }

    [Fact]
    public async Task UpdateAsync_updates_existing_lines_without_recreating_the_same_keys()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        context.Products.AddRange(Product(1, "Táo"), Product(2, "Cam"));
        context.Promotions.Add(ComboPromotion(
            10,
            "Combo cũ",
            "combo-cu",
            new ComboItemPayload { ProductId = 1, Quantity = 1, SortOrder = 0 },
            new ComboItemPayload { ProductId = 2, Quantity = 1, SortOrder = 1 }));
        await context.SaveChangesAsync();
        var service = new ComboService(new UnitOfWork(context), CreatePricing().Object,
            dbContext: context, serializer: new VersionedJsonSerializer());

        var result = await service.UpdateAsync(10, new ComboFormViewModel
        {
            Revision = 1,
            Name = "Combo mới",
            Slug = "combo-moi",
            IsActive = true,
            Items =
            [
                new ComboItemFormModel { ProductId = 2, Quantity = 3, SortOrder = 0 },
                new ComboItemFormModel { ProductId = 1, Quantity = 2, SortOrder = 1 }
            ]
        });

        Assert.True(result.Success);
        var promotion = await context.Promotions.SingleAsync(item => item.Id == 10);
        var payload = new VersionedJsonSerializer().Deserialize<ComboPayload>(promotion.PayloadJson);
        var items = payload.Items.OrderBy(item => item.SortOrder).ToList();
        Assert.Collection(items,
            item => { Assert.Equal(2, item.ProductId); Assert.Equal(3, item.Quantity); },
            item => { Assert.Equal(1, item.ProductId); Assert.Equal(2, item.Quantity); });
    }

    [Fact]
    public async Task GetActiveComboCardsAsync_batches_pricing_for_all_combos()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        var products = new[]
        {
            Product(1, "Táo"), Product(2, "Cam"), Product(3, "Nho"), Product(4, "Lê")
        };
        context.Products.AddRange(products);
        context.Promotions.AddRange(
            ComboPromotion(10, "Combo 1", "combo-1",
                new ComboItemPayload { ProductId = 1, Quantity = 1 },
                new ComboItemPayload { ProductId = 2, Quantity = 1 }),
            ComboPromotion(11, "Combo 2", "combo-2",
                new ComboItemPayload { ProductId = 3, Quantity = 1 },
                new ComboItemPayload { ProductId = 4, Quantity = 1 }));
        await context.SaveChangesAsync();
        var pricing = CreatePricing();
        var service = new ComboService(new UnitOfWork(context), pricing.Object,
            dbContext: context, serializer: new VersionedJsonSerializer());

        var cards = await service.GetActiveComboCardsAsync();

        Assert.Equal(2, cards.Count);
        pricing.Verify(service => service.GetQuotesAsync(
            It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()), Times.Once);
    }
}

public class CartComboAtomicityTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static Mock<IProductPricingService> CreatePricing()
    {
        var pricing = new Mock<IProductPricingService>();
        pricing.Setup(service => service.GetQuotesAsync(
                It.IsAny<IEnumerable<PriceTargetKey>>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((IEnumerable<PriceTargetKey> targets, DateTimeOffset? _) =>
                targets.Distinct().ToDictionary(
                    target => target,
                    target => new PriceQuote(target.ProductId, target.ProductVariantId, 50_000m, 50_000m, null)));
        return pricing;
    }

    private static Product Product(int id, int stock) => new()
    {
        Id = id,
        Name = $"Sản phẩm {id}",
        Slug = $"san-pham-{id}",
        Price = 50_000m,
        StockQuantity = stock,
        MinOrderQuantity = 1,
        IsActive = true
    };

    [Fact]
    public async Task AddItemsToCartAsync_adds_nothing_when_one_combo_item_is_unavailable()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        context.Products.AddRange(Product(1, 10), Product(2, 0));
        await context.SaveChangesAsync();
        var service = new CartService(
            new UnitOfWork(context),
            Mock.Of<ICouponService>(),
            CreatePricing().Object);

        var result = await service.AddItemsToCartAsync("combo-failure",
        [
            new CartAddItemRequest(1, 1),
            new CartAddItemRequest(2, 1)
        ]);

        Assert.False(result.Success);
        Assert.Empty(context.Carts);
        Assert.Empty(context.CartItems);
    }

    [Fact]
    public async Task AddItemsToCartAsync_adds_all_combo_items_in_one_cart()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        context.Products.AddRange(Product(1, 10), Product(2, 10));
        await context.SaveChangesAsync();
        var service = new CartService(
            new UnitOfWork(context),
            Mock.Of<ICouponService>(),
            CreatePricing().Object);

        var result = await service.AddItemsToCartAsync("combo-success",
        [
            new CartAddItemRequest(1, 2),
            new CartAddItemRequest(2, 3)
        ]);

        Assert.True(result.Success);
        var cart = Assert.Single(context.Carts);
        var items = await context.CartItems.Where(item => item.CartId == cart.Id).OrderBy(item => item.ProductId).ToListAsync();
        Assert.Collection(items,
            item => Assert.Equal(2, item.Quantity),
            item => Assert.Equal(3, item.Quantity));
    }
}
