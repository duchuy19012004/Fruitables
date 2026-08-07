using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.Services.Pricing.Combos;
using Fruitables.Services.Pricing.Coupons;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Fruitables.Services.Orders.Cart;

namespace Fruitables.Tests;

public class ComboPricingCalculatorTests
{
    public static IEnumerable<object?[]> PricingCases =>
    [
        [ComboPricingType.SumOfItems, null, null, 500_000m, 500_000m],
        [ComboPricingType.FixedPrice, 420_000m, null, 500_000m, 420_000m],
        [ComboPricingType.PercentageDiscount, null, 10m, 500_000m, 450_000m],
        [ComboPricingType.FixedDiscount, null, 75_000m, 500_000m, 425_000m]
    ];

    [Theory]
    [MemberData(nameof(PricingCases))]
    public void Calculate_returns_expected_commercial_combo_price(
        ComboPricingType type,
        decimal? fixedPrice,
        decimal? discount,
        decimal original,
        decimal expectedFinal)
    {
        var result = ComboPricingCalculator.Calculate(type, original, fixedPrice, discount);

        Assert.Equal(original, result.OriginalTotal);
        Assert.Equal(expectedFinal, result.FinalTotal);
        Assert.Equal(original - expectedFinal, result.Discount);
    }
}

public class ComboCartGroupTests
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
        pricing.Setup(service => service.GetQuoteAsync(
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((int productId, int? variantId, DateTimeOffset? _) =>
                new PriceQuote(productId, variantId, 100_000m, 100_000m, null));
        return pricing;
    }

    private static Product Product(int id) => new()
    {
        Id = id,
        Name = $"Sản phẩm {id}",
        Slug = $"san-pham-{id}",
        Price = 100_000m,
        StockQuantity = 20,
        MinOrderQuantity = 1,
        IsActive = true
    };

    private static async Task SeedComboAsync(ApplicationDbContext context)
    {
        context.Products.AddRange(Product(1), Product(2));
        context.Combos.Add(new Combo
        {
            Id = 10,
            Name = "Combo gia đình",
            Slug = "combo-gia-dinh",
            IsActive = true,
            Revision = 1,
            PricingType = ComboPricingType.FixedPrice,
            FixedPrice = 170_000m,
            AllowCouponStacking = false,
            Items =
            [
                new ComboItem { ProductId = 1, Quantity = 1, SortOrder = 0 },
                new ComboItem { ProductId = 2, Quantity = 1, SortOrder = 1 }
            ]
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddComboToCartAsync_creates_group_and_allocates_discount_exactly()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await SeedComboAsync(context);
        var service = new CartService(new UnitOfWork(context), Mock.Of<ICouponService>(), CreatePricing().Object);

        var result = await service.AddComboToCartAsync("bundle-cart", 10);

        Assert.True(result.Success);
        var group = await context.CartGroups.Include(item => item.Items).SingleAsync();
        Assert.Equal(1, group.Quantity);
        Assert.Equal(200_000m, group.OriginalTotal);
        Assert.Equal(170_000m, group.FinalTotal);
        Assert.Equal(30_000m, group.Discount);
        Assert.False(group.AllowCouponStacking);
        Assert.Equal(group.Discount, group.Items.Sum(item => item.ComboDiscount));
        Assert.Equal(group.FinalTotal, group.Items.Sum(item => item.Price * item.Quantity - item.ComboDiscount));
    }

    [Fact]
    public async Task AddComboToCartAsync_second_add_merges_same_revision_group()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await SeedComboAsync(context);
        var service = new CartService(new UnitOfWork(context), Mock.Of<ICouponService>(), CreatePricing().Object);

        Assert.True((await service.AddComboToCartAsync("bundle-merge", 10)).Success);
        Assert.True((await service.AddComboToCartAsync("bundle-merge", 10)).Success);

        var group = await context.CartGroups.Include(item => item.Items).SingleAsync();
        Assert.Equal(2, group.Quantity);
        Assert.Equal(400_000m, group.OriginalTotal);
        Assert.Equal(340_000m, group.FinalTotal);
        Assert.All(group.Items, item => Assert.Equal(2, item.Quantity));
    }

    [Fact]
    public async Task Combo_items_remain_separate_from_the_same_product_bought_individually()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await SeedComboAsync(context);
        var service = new CartService(new UnitOfWork(context), Mock.Of<ICouponService>(), CreatePricing().Object);

        Assert.True((await service.AddToCartAsync("bundle-separate", 1, 1)).Success);
        Assert.True((await service.AddComboToCartAsync("bundle-separate", 10)).Success);

        var cart = await context.Carts.SingleAsync();
        var appleLines = await context.CartItems
            .Where(item => item.CartId == cart.Id && item.ProductId == 1)
            .OrderBy(item => item.CartGroupId)
            .ToListAsync();
        Assert.Equal(2, appleLines.Count);
        Assert.Contains(appleLines, item => item.CartGroupId == null);
        Assert.Contains(appleLines, item => item.CartGroupId != null);
    }

    [Fact]
    public async Task GetCartAsync_marks_old_group_invalid_when_combo_revision_changes()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await SeedComboAsync(context);
        var service = new CartService(new UnitOfWork(context), Mock.Of<ICouponService>(), CreatePricing().Object);
        Assert.True((await service.AddComboToCartAsync("bundle-revision", 10)).Success);

        var combo = await context.Combos.FindAsync(10);
        combo!.Revision = 2;
        await context.SaveChangesAsync();

        var cart = await service.GetCartAsync("bundle-revision");

        Assert.Single(cart.Groups);
        Assert.False(cart.Groups[0].IsValid);
        Assert.All(cart.Groups[0].Items, item => Assert.False(item.IsAvailable));
    }

    [Fact]
    public async Task ApplyCouponAsync_excludes_combo_that_disallows_stacking()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await SeedComboAsync(context);
        var coupon = new Mock<ICouponService>();
        coupon.Setup(service => service.ApplyCouponAsync("SAVE", 0m, 0))
            .ReturnsAsync(new CouponApplyResult { Success = false, ErrorMessage = "Không có sản phẩm hợp lệ" });
        var service = new CartService(new UnitOfWork(context), coupon.Object, CreatePricing().Object);
        Assert.True((await service.AddComboToCartAsync("bundle-coupon", 10)).Success);

        var result = await service.ApplyCouponAsync("bundle-coupon", "SAVE");

        Assert.False(result.Success);
        coupon.Verify(service => service.ApplyCouponAsync("SAVE", 0m, 0), Times.Once);
    }
}
