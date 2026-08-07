using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Pricing.ProductPricing;

namespace Fruitables.Tests;

public class ProductServicePricingTests
{
    [Fact]
    public async Task GetAllProducts_applies_schedule_prices_through_mandatory_pricing()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        var now = DateTimeOffset.UtcNow;
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit" });
        context.Products.Add(new Product
        {
            Id = 1,
            CategoryId = 1,
            Name = "Táo",
            Slug = "tao",
            Price = 100_000,
            IsActive = true,
            PriceSchedules =
            [
                new PriceSchedule
                {
                    Id = 3,
                    ProductId = 1,
                    DiscountType = DiscountType.FixedPrice,
                    Value = 80_000,
                    StartsAt = now.AddHours(-1),
                    EndsAt = now.AddHours(1)
                }
            ]
        });
        await context.SaveChangesAsync();

        var pricing = new ProductPricingService(context, TimeProvider.System);
        var service = new ProductService(context, TimeProvider.System, pricing);

        var products = await service.GetAllProductsAsync();

        var product = Assert.Single(products);
        Assert.Equal(80_000, product.DisplayMinPrice);
        Assert.Equal(80_000, product.SalePrice);
    }

    [Fact]
    public async Task GetShopViewModel_materializes_catalog_pricing_once()
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        await using var context = new ApplicationDbContext(options);
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit-once" });
        context.Products.Add(new Product
        {
            Id = 1,
            CategoryId = 1,
            Name = "Táo once",
            Slug = "tao-once",
            Price = 100_000,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var pricing = new ProductPricingService(context, TimeProvider.System);
        var service = new ProductService(context, TimeProvider.System, pricing);

        await service.GetShopViewModelAsync(null, null, null, null, null, 1, 9);

        Assert.True(interceptor.ProductSelectCount < 5);
    }

    [Fact]
    public async Task GetShopViewModel_pages_common_catalog_sort_before_loading_price_graph()
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        await using var context = new ApplicationDbContext(options);
        context.Categories.Add(new Category { Id = 1, Name = "Fruit", Slug = "fruit-page" });
        context.Products.AddRange(Enumerable.Range(1, 12).Select(id => new Product
        {
            Id = id,
            CategoryId = 1,
            Name = $"Fruit {id}",
            Slug = $"fruit-page-{id}",
            Price = 100_000 + id,
            IsActive = true
        }));
        await context.SaveChangesAsync();

        var service = new ProductService(
            context,
            TimeProvider.System,
            new ProductPricingService(context, TimeProvider.System));

        var result = await service.GetShopViewModelAsync(null, null, null, null, null, 1, 1);

        Assert.Single(result.Products);
        Assert.Contains(interceptor.SelectCommands, command =>
            command.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
    }
}
