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

        var unitOfWork = new UnitOfWork(context);
        var pricing = new ProductPricingService(unitOfWork, TimeProvider.System);
        var service = new ProductService(unitOfWork, TimeProvider.System, pricing);

        var products = await service.GetAllProductsAsync();

        var product = Assert.Single(products);
        Assert.Equal(80_000, product.DisplayMinPrice);
        Assert.Equal(80_000, product.SalePrice);
    }
}
