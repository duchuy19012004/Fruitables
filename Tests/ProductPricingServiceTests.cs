using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public class ProductPricingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 2, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(DiscountType.FixedPrice, 80_000, 80_000)]
    [InlineData(DiscountType.Percentage, 15, 85_000)]
    public void Quote_applies_active_fixed_or_percentage_schedule(
        DiscountType type,
        decimal value,
        decimal expected)
    {
        var product = new Product { Id = 1, Price = 100_000 };
        var schedule = new PriceSchedule
        {
            ProductId = 1,
            DiscountType = type,
            Value = value,
            StartsAt = Now.AddHours(-1),
            EndsAt = Now.AddHours(1)
        };

        var quote = ProductPricingService.CalculateQuote(product.Price, [schedule], Now);

        Assert.Equal(expected, quote.EffectivePrice);
        Assert.Equal(100_000, quote.BasePrice);
        Assert.True(quote.IsDiscounted);
    }

    [Fact]
    public void Quote_uses_half_open_schedule_boundaries()
    {
        var schedule = new PriceSchedule
        {
            ProductId = 1,
            DiscountType = DiscountType.FixedPrice,
            Value = 50_000,
            StartsAt = Now,
            EndsAt = Now.AddHours(1)
        };

        Assert.Equal(50_000, ProductPricingService.CalculateQuote(100_000, [schedule], Now).EffectivePrice);
        Assert.Equal(100_000, ProductPricingService.CalculateQuote(100_000, [schedule], Now.AddHours(1)).EffectivePrice);
    }

    [Fact]
    public async Task GetQuoteAsync_prices_a_variant_independently_from_its_product()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var product = new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 };
        var variant = new ProductVariant { Id = 2, ProductId = 1, Name = "Hộp 2kg", SKU = "TAO-2", Price = 180_000 };
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        context.PriceSchedules.Add(new PriceSchedule
        {
            ProductId = 1,
            ProductVariantId = 2,
            DiscountType = DiscountType.Percentage,
            Value = 10,
            StartsAt = Now.AddDays(-1),
            EndsAt = null
        });
        await context.SaveChangesAsync();

        var service = new ProductPricingService(new UnitOfWork(context), new FixedTimeProvider(Now));
        var quote = await service.GetQuoteAsync(1, 2);

        Assert.NotNull(quote);
        Assert.Equal(162_000, quote.EffectivePrice);
        Assert.Equal(2, quote.ProductVariantId);
    }

    [Fact]
    public void Catalog_projection_translates_to_one_sql_query_with_scheduled_prices()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=FruitablesProjectionTest;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);
        var service = new ProductPricingService(new UnitOfWork(context), new FixedTimeProvider(Now));

        var sql = service.ProjectCatalogPrices(context.Products.Where(p => p.IsActive)).ToQueryString();

        Assert.Contains("PriceSchedules", sql);
        Assert.Contains("ProductVariants", sql);
        Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
