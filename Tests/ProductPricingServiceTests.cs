using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.Services.Pricing.ProductPricing;
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
    public async Task Catalog_projection_computes_scheduled_prices_for_products_and_variants()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Products.AddRange(
            new Product
            {
                Id = 1,
                Name = "Tao",
                Slug = "tao",
                Price = 100_000,
                IsActive = true,
                PriceSchedules =
                [
                    new PriceSchedule
                    {
                        ProductId = 1,
                        DiscountType = DiscountType.FixedPrice,
                        Value = 80_000,
                        StartsAt = Now.AddHours(-1),
                        EndsAt = Now.AddHours(1)
                    }
                ]
            },
            new Product
            {
                Id = 2,
                Name = "Cam",
                Slug = "cam",
                Price = 50_000,
                IsActive = true,
                Variants =
                [
                    new ProductVariant
                    {
                        Id = 3,
                        ProductId = 2,
                        Name = "Hop 1kg",
                        SKU = "CAM-1",
                        Price = 120_000,
                        IsActive = true,
                        PriceSchedules =
                        [
                            new PriceSchedule
                            {
                                ProductId = 2,
                                ProductVariantId = 3,
                                DiscountType = DiscountType.Percentage,
                                Value = 25,
                                StartsAt = Now.AddHours(-1),
                                EndsAt = Now.AddHours(1)
                            }
                        ]
                    },
                    new ProductVariant
                    {
                        Id = 4,
                        ProductId = 2,
                        Name = "Hop 2kg",
                        SKU = "CAM-2",
                        Price = 200_000,
                        IsActive = true
                    }
                ]
            });
        await context.SaveChangesAsync();

        var service = new ProductPricingService(new UnitOfWork(context), new FixedTimeProvider(Now));

        var projections = service.ProjectCatalogPrices(context.Products.Where(p => p.IsActive))
            .OrderBy(p => p.ProductId)
            .ToList();

        Assert.Collection(projections,
            product =>
            {
                Assert.Equal(1, product.ProductId);
                Assert.Equal(80_000, product.MinPrice);
                Assert.Equal(80_000, product.MaxPrice);
            },
            product =>
            {
                Assert.Equal(2, product.ProductId);
                Assert.Equal(90_000, product.MinPrice);
                Assert.Equal(200_000, product.MaxPrice);
            });
    }

    [Fact]
    public void CalculateQuote_when_legacy_data_has_two_active_schedules_uses_latest_start_then_highest_id()
    {
        var schedules = new[]
        {
            new PriceSchedule
            {
                Id = 10,
                ProductId = 1,
                DiscountType = DiscountType.FixedPrice,
                Value = 80_000,
                StartsAt = Now.AddHours(-2)
            },
            new PriceSchedule
            {
                Id = 11,
                ProductId = 1,
                DiscountType = DiscountType.FixedPrice,
                Value = 70_000,
                StartsAt = Now.AddHours(-1)
            },
            new PriceSchedule
            {
                Id = 12,
                ProductId = 1,
                DiscountType = DiscountType.FixedPrice,
                Value = 60_000,
                StartsAt = Now.AddHours(-1)
            }
        };

        var quote = PriceCalculator.CalculateQuote(100_000, schedules, Now);

        Assert.Equal(60_000, quote.EffectivePrice);
        Assert.Equal(12, quote.ScheduleId);
    }

    [Fact]
    public void CalculateQuote_percentage_rounds_vnd_away_from_zero()
    {
        var schedule = new PriceSchedule
        {
            Id = 21,
            ProductId = 1,
            DiscountType = DiscountType.Percentage,
            Value = 15,
            StartsAt = Now.AddMinutes(-1)
        };

        var quote = PriceCalculator.CalculateQuote(99_999, new[] { schedule }, Now);

        Assert.Equal(84_999, quote.EffectivePrice);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
