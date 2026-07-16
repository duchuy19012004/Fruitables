using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Fruitables.Tests;

public class CartVariantPricingTests
{
    [Fact]
    public async Task Cart_keeps_variants_as_separate_lines_and_uses_their_effective_prices()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 50_000, StockQuantity = 99 });
        context.ProductVariants.AddRange(
            new ProductVariant { Id = 11, ProductId = 1, Name = "1kg", SKU = "TAO-1", Price = 100_000, StockQuantity = 5 },
            new ProductVariant { Id = 12, ProductId = 1, Name = "2kg", SKU = "TAO-2", Price = 180_000, StockQuantity = 3 });
        context.PriceSchedules.Add(new PriceSchedule
        {
            ProductId = 1, ProductVariantId = 12, DiscountType = DiscountType.Percentage, Value = 10,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = null
        });
        await context.SaveChangesAsync();
        var uow = new UnitOfWork(context);
        var pricing = new ProductPricingService(uow, TimeProvider.System);
        var cart = new CartService(uow, Mock.Of<ICouponService>(), pricing);

        await cart.AddToCartAsync("session", 1, 1, 11);
        await cart.AddToCartAsync("session", 1, 1, 12);
        var result = await cart.GetCartAsync("session");

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.ProductVariantId == 11 && i.Price == 100_000 && i.StockQuantity == 5);
        Assert.Contains(result.Items, i => i.ProductVariantId == 12 && i.Price == 162_000 && i.StockQuantity == 3);

        var first = result.Items.Single(i => i.ProductVariantId == 11);
        var second = result.Items.Single(i => i.ProductVariantId == 12);
        await cart.UpdateQuantityAsync("session", second.CartItemId, 2);
        var updated = await cart.GetCartAsync("session");
        Assert.Equal(1, updated.Items.Single(i => i.CartItemId == first.CartItemId).Quantity);
        Assert.Equal(2, updated.Items.Single(i => i.CartItemId == second.CartItemId).Quantity);
    }
}
