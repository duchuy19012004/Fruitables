using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.ViewModels;
using Xunit;

namespace Fruitables.Tests;

public class SalesAnalyticsServiceTests
{
    [Fact]
    public async Task GetHubAsync_Overview_ComputesGrossAndNetForPeriod()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        var createdAt = new DateTime(2026, 7, 5, 12, 0, 0);
        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-DEL",
                CreatedAt = createdAt,
                Total = 100m,
                Subtotal = 100m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Delivered
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-PROC",
                CreatedAt = createdAt,
                Total = 50m,
                Subtotal = 50m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Processing
            },
            new Order
            {
                Id = 3,
                OrderNumber = "ORD-REF",
                CreatedAt = createdAt,
                Total = 20m,
                Subtotal = 20m,
                PaymentStatus = PaymentStatus.Refunded,
                Status = OrderStatus.Returned
            },
            new Order
            {
                Id = 4,
                OrderNumber = "ORD-CAN",
                CreatedAt = createdAt,
                Total = 10m,
                Subtotal = 10m,
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.Cancelled
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Overview
        };

        var uow = new UnitOfWork(ctx);
        var sut = new SalesAnalyticsService(uow);
        var hub = await sut.GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.NotNull(hub.Overview);
        Assert.Equal(150, hub.Overview!.Gross.Value); // 100+50 paid
        Assert.Equal(80, hub.Overview.Net.Value);     // 100 - 20
    }

    [Fact]
    public async Task GetHubAsync_Merch_RanksProductByNetLineRevenue_Share100WhenSingle()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.Add(new Category { Id = 1, Name = "Trái cây", Slug = "trai-cay" });
        ctx.Products.Add(new Product
        {
            Id = 10,
            CategoryId = 1,
            Name = "Táo Fuji",
            Slug = "tao-fuji",
            Price = 50m
        });

        var createdAt = new DateTime(2026, 7, 5, 12, 0, 0);
        ctx.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-DEL",
            CreatedAt = createdAt,
            Total = 150m,
            Subtotal = 150m,
            PaymentStatus = PaymentStatus.Paid,
            Status = OrderStatus.Delivered
        });
        ctx.OrderItems.Add(new OrderItem
        {
            OrderId = 1,
            ProductId = 10,
            ProductName = "Táo Fuji",
            Quantity = 3,
            Price = 50m,
            Total = 150m
        });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Merch,
            Dimension = MerchDimension.Product,
            Take = 50
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.NotNull(hub.Merch);
        Assert.Single(hub.Merch!.Rows);

        var row = hub.Merch.Rows[0];
        Assert.Equal(1, row.Rank);
        Assert.Equal(10, row.ProductId);
        Assert.Equal("Táo Fuji", row.Name);
        Assert.Equal("Trái cây", row.CategoryName);
        Assert.Equal(150m, row.NetRevenue); // 50 * 3
        Assert.Equal(100m, row.SharePercent);
        Assert.Equal(3, row.Units);
        Assert.Equal(1, row.OrderCount);

        Assert.NotEmpty(hub.Merch.RankBar.Labels);
        Assert.Equal("Táo Fuji", hub.Merch.RankBar.Labels[0]);
        Assert.Equal(150m, hub.Merch.RankBar.Datasets[0].Data[0]);
        Assert.NotEmpty(hub.Merch.CategoryMix.Labels);
    }

    [Fact]
    public async Task GetHubAsync_Merch_ComputesDeltaPercentVsPreviousPeriod()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.Add(new Category { Id = 1, Name = "Fruits", Slug = "fruits" });
        ctx.Products.Add(new Product
        {
            Id = 10,
            CategoryId = 1,
            Name = "Apple",
            Slug = "apple",
            Price = 10m
        });

        // Current: Jul 1–15 (15 days) → previous: Jun 16–30
        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-PREV",
                CreatedAt = new DateTime(2026, 6, 20, 10, 0, 0),
                Total = 100m,
                Subtotal = 100m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Delivered
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-CUR",
                CreatedAt = new DateTime(2026, 7, 5, 10, 0, 0),
                Total = 150m,
                Subtotal = 150m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Delivered
            });
        ctx.OrderItems.AddRange(
            new OrderItem
            {
                OrderId = 1,
                ProductId = 10,
                ProductName = "Apple",
                Quantity = 10,
                Price = 10m,
                Total = 100m
            },
            new OrderItem
            {
                OrderId = 2,
                ProductId = 10,
                ProductName = "Apple",
                Quantity = 15,
                Price = 10m,
                Total = 150m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 15),
            Tab = SalesAnalyticsTab.Merch,
            Dimension = MerchDimension.Product
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.NotNull(hub.Merch);
        var row = Assert.Single(hub.Merch!.Rows);
        Assert.Equal(150m, row.NetRevenue);
        // (150 - 100) / 100 * 100 = 50%
        Assert.Equal(50m, row.DeltaPercent);
    }

    [Fact]
    public async Task GetHubAsync_Merch_CategoryDimension_GroupsByCategory()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.AddRange(
            new Category { Id = 1, Name = "Fruits", Slug = "fruits" },
            new Category { Id = 2, Name = "Veggies", Slug = "veggies" });
        ctx.Products.AddRange(
            new Product { Id = 10, CategoryId = 1, Name = "Apple", Slug = "apple", Price = 10m },
            new Product { Id = 11, CategoryId = 1, Name = "Banana", Slug = "banana", Price = 5m },
            new Product { Id = 12, CategoryId = 2, Name = "Carrot", Slug = "carrot", Price = 20m });

        var createdAt = new DateTime(2026, 7, 5, 12, 0, 0);
        ctx.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-1",
            CreatedAt = createdAt,
            Total = 55m,
            Subtotal = 55m,
            PaymentStatus = PaymentStatus.Paid,
            Status = OrderStatus.Delivered
        });
        ctx.OrderItems.AddRange(
            new OrderItem { OrderId = 1, ProductId = 10, ProductName = "Apple", Quantity = 2, Price = 10m, Total = 20m },
            new OrderItem { OrderId = 1, ProductId = 11, ProductName = "Banana", Quantity = 3, Price = 5m, Total = 15m },
            new OrderItem { OrderId = 1, ProductId = 12, ProductName = "Carrot", Quantity = 1, Price = 20m, Total = 20m });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Merch,
            Dimension = MerchDimension.Category
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.NotNull(hub.Merch);
        Assert.Equal(2, hub.Merch!.Rows.Count);
        Assert.Equal("Fruits", hub.Merch.Rows[0].Name);
        Assert.Equal(35m, hub.Merch.Rows[0].NetRevenue); // 20+15
        Assert.Equal(1, hub.Merch.Rows[0].CategoryId);
        Assert.Null(hub.Merch.Rows[0].ProductId);
    }

    [Fact]
    public async Task GetHubAsync_Overview_FillsTopProductsAndCategories()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.Add(new Category { Id = 1, Name = "Fruits", Slug = "fruits" });
        ctx.Products.Add(new Product
        {
            Id = 10,
            CategoryId = 1,
            Name = "Apple",
            Slug = "apple",
            Price = 25m
        });
        ctx.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-1",
            CreatedAt = new DateTime(2026, 7, 5, 12, 0, 0),
            Total = 50m,
            Subtotal = 50m,
            PaymentStatus = PaymentStatus.Paid,
            Status = OrderStatus.Delivered
        });
        ctx.OrderItems.Add(new OrderItem
        {
            OrderId = 1,
            ProductId = 10,
            ProductName = "Apple",
            Quantity = 2,
            Price = 25m,
            Total = 50m
        });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Overview
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.NotNull(hub.Overview);
        Assert.Single(hub.Overview!.TopProducts);
        Assert.Equal(50m, hub.Overview.TopProducts[0].NetRevenue);
        Assert.Single(hub.Overview.TopCategories);
        Assert.Equal("Fruits", hub.Overview.TopCategories[0].Name);
        Assert.NotEmpty(hub.Overview.TopProductsBar.Labels);
        Assert.NotEmpty(hub.Overview.CategoryMix.Labels);
    }

    [Fact]
    public async Task GetHubAsync_Cancellations_ComputesKpisReasonsAndValueByProduct()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.Add(new Category { Id = 1, Name = "Fruits", Slug = "fruits" });
        ctx.Products.Add(new Product
        {
            Id = 10,
            CategoryId = 1,
            Name = "Apple",
            Slug = "apple",
            Price = 40m
        });

        var day = new DateTime(2026, 7, 5, 12, 0, 0);
        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-OK",
                CreatedAt = day,
                Total = 100m,
                Subtotal = 100m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Delivered
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-CAN1",
                CreatedAt = day,
                Total = 80m,
                Subtotal = 80m,
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.Cancelled,
                CancelReason = "Đổi ý"
            },
            new Order
            {
                Id = 3,
                OrderNumber = "ORD-CAN2",
                CreatedAt = day,
                Total = 40m,
                Subtotal = 40m,
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.Cancelled,
                CancelReason = null
            });
        ctx.OrderItems.AddRange(
            new OrderItem
            {
                OrderId = 2,
                ProductId = 10,
                ProductName = "Apple",
                Quantity = 2,
                Price = 40m,
                Total = 80m
            },
            new OrderItem
            {
                OrderId = 3,
                ProductId = 10,
                ProductName = "Apple",
                Quantity = 1,
                Price = 40m,
                Total = 40m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Cancellations
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.NotNull(hub.Cancellations);

        var c = hub.Cancellations!;
        Assert.Equal(2, c.CancelledCount.Value);
        Assert.Equal(Math.Round(2m / 3m * 100m, 2), c.CancelRate.Value);
        Assert.Equal(120m, c.CancelledValue.Value); // 80 + 40 order totals

        Assert.Contains("Đổi ý", c.Reasons.Labels);
        Assert.Contains("Không ghi rõ", c.Reasons.Labels);

        Assert.NotEmpty(c.ValueByProduct.Labels);
        Assert.Equal("Apple", c.ValueByProduct.Labels[0]);
        Assert.Equal(120m, c.ValueByProduct.Datasets[0].Data[0]); // 80 + 40 line nets

        Assert.NotEmpty(c.CancelTrend.Labels);
        Assert.NotEmpty(c.ValueByCategory.Labels);
    }

    [Fact]
    public async Task GetHubAsync_Merch_IgnoresNonDeliveredPaidOrders()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.Add(new Category { Id = 1, Name = "Fruits", Slug = "fruits" });
        ctx.Products.Add(new Product
        {
            Id = 10,
            CategoryId = 1,
            Name = "Apple",
            Slug = "apple",
            Price = 10m
        });

        var day = new DateTime(2026, 7, 5, 12, 0, 0);
        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-PROC",
                CreatedAt = day,
                Total = 100m,
                Subtotal = 100m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Processing
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-DEL",
                CreatedAt = day,
                Total = 30m,
                Subtotal = 30m,
                PaymentStatus = PaymentStatus.Paid,
                Status = OrderStatus.Delivered
            });
        ctx.OrderItems.AddRange(
            new OrderItem
            {
                OrderId = 1,
                ProductId = 10,
                ProductName = "Apple",
                Quantity = 10,
                Price = 10m,
                Total = 100m
            },
            new OrderItem
            {
                OrderId = 2,
                ProductId = 10,
                ProductName = "Apple",
                Quantity = 3,
                Price = 10m,
                Total = 30m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Merch,
            Dimension = MerchDimension.Product
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        var row = Assert.Single(hub.Merch!.Rows);
        Assert.Equal(30m, row.NetRevenue);
        Assert.Equal(3, row.Units);
    }
}

