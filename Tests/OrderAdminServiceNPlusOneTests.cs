using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Fruitables.Services.Orders.OrderManagement;

namespace Fruitables.Tests;

// Guardrail N+1: pre-dispatch cancellation must batch product lookups. Terminal
// orders must be rejected before any product query.
public class OrderAdminServiceNPlusOneTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task UpdateOrderStatusAsync_CancelOrder_ProductLookupIsBatched(int itemCount)
    {
        var (interceptor, options, products) = await SeedAsync(itemCount);

        var order = CreateOrder(products, itemCount, OrderStatus.Processing);
        using var context = new ApplicationDbContext(options);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var adminService = new OrderAdminService(context, Mock.Of<IOrderLogService>(), Mock.Of<IRealtimeNotifier>());

        var result = await adminService.UpdateOrderStatusAsync(new UpdateOrderStatusRequest
        {
            OrderId = order.Id,
            NewStatus = OrderStatus.Cancelled,
            AdminId = 100,
            Notes = "Bulk cancel"
        });

        Assert.True(result.Success);
        // One batched product SELECT for the restore path — must not scale with itemCount.
        Assert.Equal(1, interceptor.GetCount("Products"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task UpdateOrderStatusAsync_CancelledOrderIsTerminal_ProductsAreNotQueried(int itemCount)
    {
        var (interceptor, options, products) = await SeedAsync(itemCount);

        var order = CreateOrder(products, itemCount, OrderStatus.Cancelled);
        using var context = new ApplicationDbContext(options);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var adminService = new OrderAdminService(context, Mock.Of<IOrderLogService>(), Mock.Of<IRealtimeNotifier>());

        var result = await adminService.UpdateOrderStatusAsync(new UpdateOrderStatusRequest
        {
            OrderId = order.Id,
            NewStatus = OrderStatus.Processing,
            AdminId = 100,
            Notes = "Cancelled orders cannot reopen"
        });

        Assert.False(result.Success);
        Assert.Equal(OrderErrorType.InvalidStatusTransition, result.ErrorType);
        Assert.Equal(0, interceptor.GetCount("Products"));
    }

    private static async Task<(CountingQueryInterceptor interceptor, DbContextOptions<ApplicationDbContext> options, List<Product> products)>
        SeedAsync(int productCount)
    {
        var interceptor = new CountingQueryInterceptor();
        var options = TestDbContextFactory.CreateSqliteOptions(interceptor);
        using var context = new ApplicationDbContext(options);

        // Admin user is required because OrderStatusHistory.AdminId is an FK to User.
        context.Users.Add(new User
        {
            Id = 100,
            Name = "Test Admin",
            Email = "admin@example.com",
            Password = "hashed",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Categories.Add(new Category { Id = 1, Name = "Default", Slug = "default" });

        var products = new List<Product>(productCount);
        for (int i = 1; i <= productCount; i++)
        {
            var product = new Product
            {
                Id = i,
                CategoryId = 1,
                Name = $"P{i}",
                Slug = $"p{i}",
                Price = 10,
                StockQuantity = 100,
                MinOrderQuantity = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            products.Add(product);
            context.Products.Add(product);
        }
        await context.SaveChangesAsync();

        return (interceptor, options, products);
    }

    private static Order CreateOrder(List<Product> products, int itemCount, OrderStatus status)
    {
        var order = new Order
        {
            Id = 10_000 + itemCount,
            OrderNumber = $"ORD-N1-{itemCount}",
            Status = status,
            Subtotal = 10m * itemCount,
            Total = 10m * itemCount,
            PaymentMethod = PaymentMethod.COD,
            PaymentStatus = PaymentStatus.Pending,
            Items = new List<OrderItem>(itemCount)
        };
        for (int i = 0; i < itemCount; i++)
        {
            var product = products[i];
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = 1,
                Price = product.Price,
                Total = product.Price
            });
        }
        return order;
    }
}
