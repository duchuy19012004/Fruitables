using Fruitables.Data;
using Fruitables.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Fruitables.Tests;

public class PricingModelConfigurationTests
{
    [Fact]
    public void Quantity_columns_use_decimal_precision()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var properties = new[]
        {
            (typeof(Product), nameof(Product.StockQuantity)),
            (typeof(Product), nameof(Product.MinOrderQuantity)),
            (typeof(ProductVariant), nameof(ProductVariant.StockQuantity)),
            (typeof(CartItem), nameof(CartItem.Quantity)),
            (typeof(OrderItem), nameof(OrderItem.Quantity)),
            (typeof(ComboItem), nameof(ComboItem.Quantity)),
            (typeof(Coupon), nameof(Coupon.MinQuantity))
        };

        foreach (var (entityType, propertyName) in properties)
        {
            var property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;
            Assert.Equal(10, property.GetPrecision());
            Assert.Equal(2, property.GetScale());
        }
    }

    [Fact]
    public void Revision_columns_default_to_one_and_only_schedule_revision_is_concurrency_token()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        var productRevision = context.Model
            .FindEntityType(typeof(Product))!
            .FindProperty(nameof(Product.PriceRevision))!;

        var variantRevision = context.Model
            .FindEntityType(typeof(ProductVariant))!
            .FindProperty(nameof(ProductVariant.PriceRevision))!;

        var scheduleRevision = context.Model
            .FindEntityType(typeof(PriceSchedule))!
            .FindProperty(nameof(PriceSchedule.Revision))!;

        Assert.Equal(1, productRevision.GetDefaultValue());
        Assert.False(productRevision.IsConcurrencyToken);

        Assert.Equal(1, variantRevision.GetDefaultValue());
        Assert.False(variantRevision.IsConcurrencyToken);

        Assert.Equal(1, scheduleRevision.GetDefaultValue());
        Assert.True(scheduleRevision.IsConcurrencyToken);
    }

    [Fact]
    public void Cart_session_id_has_a_unique_filtered_index()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var index = context.Model
            .FindEntityType(typeof(Cart))!
            .GetIndexes()
            .Single(item => item.Properties.Single().Name == nameof(Cart.SessionId));

        Assert.True(index.IsUnique);
        Assert.Equal("[SessionId] IS NOT NULL", index.GetFilter());
    }
}
