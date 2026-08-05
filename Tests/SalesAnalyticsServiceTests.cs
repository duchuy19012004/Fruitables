using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Xunit;
using Fruitables.Services.Analytics.Sales;

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
                Status = OrderStatus.Cancelled
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
        Assert.Equal(2, c.CancelTrend.Datasets.Count);
        Assert.Equal("Đơn hủy", c.CancelTrend.Datasets[0].Label);
        Assert.Equal("Tỷ lệ hủy %", c.CancelTrend.Datasets[1].Label);
        Assert.NotEmpty(c.ValueByCategory.Labels);
    }

    [Fact]
    public async Task GetHubAsync_Merch_SortByUnitsAsc_OrdersAscending()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.Add(new Category { Id = 1, Name = "Fruits", Slug = "fruits" });
        ctx.Products.AddRange(
            new Product { Id = 10, CategoryId = 1, Name = "HighUnits", Slug = "high", Price = 5m },
            new Product { Id = 11, CategoryId = 1, Name = "LowUnits",  Slug = "low",  Price = 50m });

        var day = new DateTime(2026, 7, 5, 12, 0, 0);
        ctx.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-1",
            CreatedAt = day,
            Total = 160m,
            Subtotal = 160m,
            PaymentStatus = PaymentStatus.Paid,
            Status = OrderStatus.Delivered
        });
        // HighUnits: 10 × 5 = 50 net, LowUnits: 2 × 50 = 100 net → default net order puts LowUnits first
        ctx.OrderItems.AddRange(
            new OrderItem { OrderId = 1, ProductId = 10, ProductName = "HighUnits", Quantity = 10, Price = 5m, Total = 50m },
            new OrderItem { OrderId = 1, ProductId = 11, ProductName = "LowUnits",  Quantity = 2,  Price = 50m, Total = 100m });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 16),
            Tab = SalesAnalyticsTab.Merch,
            Dimension = MerchDimension.Product,
            Sort = "units",
            Dir = "asc",
            Take = 50
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.NotNull(hub.Merch);
        Assert.Equal(2, hub.Merch!.Rows.Count);
        Assert.Equal("LowUnits", hub.Merch.Rows[0].Name);
        Assert.Equal(2, hub.Merch.Rows[0].Units);
        Assert.Equal("HighUnits", hub.Merch.Rows[1].Name);
        Assert.Equal(10, hub.Merch.Rows[1].Units);
    }

    [Fact]
    public async Task GetHubAsync_Cancellations_CancelTrend_HasCountAndRateDatasets()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

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
                OrderNumber = "ORD-CAN",
                CreatedAt = day,
                Total = 50m,
                Subtotal = 50m,
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.Cancelled
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 7, 5),
            To = new DateTime(2026, 7, 5),
            Tab = SalesAnalyticsTab.Cancellations
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.NotNull(hub.Cancellations);
        var trend = hub.Cancellations!.CancelTrend;
        Assert.Equal(2, trend.Datasets.Count);
        Assert.Equal("Đơn hủy", trend.Datasets[0].Label);
        Assert.Equal("Tỷ lệ hủy %", trend.Datasets[1].Label);
        Assert.Single(trend.Labels);
        Assert.Equal(1m, trend.Datasets[0].Data[0]); // 1 cancelled
        Assert.Equal(50m, trend.Datasets[1].Data[0]); // 1/2 * 100
    }

    [Fact]
    public async Task GetHubAsync_Merch_GrowthChart_SkipsNullDeltaProducts()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Categories.Add(new Category { Id = 1, Name = "Fruits", Slug = "fruits" });
        ctx.Products.Add(new Product
        {
            Id = 10,
            CategoryId = 1,
            Name = "BrandNew",
            Slug = "brand-new",
            Price = 10m
        });

        // Only current period sales → previous = 0 → DeltaPercent null
        ctx.Orders.Add(new Order
        {
            Id = 1,
            OrderNumber = "ORD-CUR",
            CreatedAt = new DateTime(2026, 7, 5, 10, 0, 0),
            Total = 100m,
            Subtotal = 100m,
            PaymentStatus = PaymentStatus.Paid,
            Status = OrderStatus.Delivered
        });
        ctx.OrderItems.Add(new OrderItem
        {
            OrderId = 1,
            ProductId = 10,
            ProductName = "BrandNew",
            Quantity = 10,
            Price = 10m,
            Total = 100m
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
        Assert.Single(hub.Merch!.Rows);
        Assert.Null(hub.Merch.Rows[0].DeltaPercent);
        // Growth chart must omit null-delta rows (not plot as 0)
        Assert.Empty(hub.Merch.Growth.Labels);
        Assert.Empty(hub.Merch.Growth.Datasets[0].Data);
    }

    [Fact]
    public async Task ExportExcelAsync_InvalidRange_ThrowsArgumentException()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);
        var sut = new SalesAnalyticsService(new UnitOfWork(ctx));

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2025, 1, 1),
            To = new DateTime(2026, 2, 2),
            Tab = SalesAnalyticsTab.Overview
        };

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExportExcelAsync(filter));
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

    // ── Period boundary guardrails (migrated from legacy Revenue/Cancel services) ──
    // Order.CreatedAt is stored Vietnam local time. Custom ranges use half-open
    // [From.Date, To.Date+1day); evening orders on the end date must be included.

    [Fact]
    public async Task GetHubAsync_CustomSingleDay_IncludesEveningVietnamOrders()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-EVE",
                CreatedAt = new DateTime(2026, 6, 3, 20, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 100m,
                Discount = 0m,
                Total = 100m
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-EAR",
                CreatedAt = new DateTime(2026, 6, 3, 2, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 50m,
                Discount = 0m,
                Total = 50m
            },
            new Order
            {
                Id = 3,
                OrderNumber = "ORD-NXT",
                CreatedAt = new DateTime(2026, 6, 4, 1, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 30m,
                Discount = 0m,
                Total = 30m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 6, 3),
            To = new DateTime(2026, 6, 3),
            Tab = SalesAnalyticsTab.Overview
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.NotNull(hub.Overview);
        // Net = delivered paid − refunded; both Jun 3 orders only (not Jun 4).
        Assert.Equal(150m, hub.Overview!.Net.Value);
        Assert.Equal(150m, hub.Overview.Gross.Value);
    }

    [Fact]
    public async Task GetHubAsync_CustomRange_ExcludesOrdersOutsideRange()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-IN",
                CreatedAt = new DateTime(2026, 6, 3, 12, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 100m,
                Discount = 0m,
                Total = 100m
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-BEFORE",
                CreatedAt = new DateTime(2026, 5, 31, 23, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 200m,
                Discount = 0m,
                Total = 200m
            },
            new Order
            {
                Id = 3,
                OrderNumber = "ORD-AFTER",
                CreatedAt = new DateTime(2026, 6, 5, 1, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 300m,
                Discount = 0m,
                Total = 300m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 6, 2),
            To = new DateTime(2026, 6, 4),
            Tab = SalesAnalyticsTab.Overview
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.Equal(100m, hub.Overview!.Net.Value);
        Assert.Equal(100m, hub.Overview.Gross.Value);
    }

    [Fact]
    public async Task GetHubAsync_Overview_GrossTrend_BucketsEveningOrderOnSameDay()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-EVE",
                CreatedAt = new DateTime(2026, 6, 3, 20, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 100m,
                Discount = 0m,
                Total = 100m
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-NXT",
                CreatedAt = new DateTime(2026, 6, 4, 2, 0, 0),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Paid,
                Subtotal = 50m,
                Discount = 0m,
                Total = 50m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 6, 3),
            To = new DateTime(2026, 6, 4),
            Tab = SalesAnalyticsTab.Overview
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.NotNull(hub.Overview);
        var trend = hub.Overview!.Trend;
        Assert.Equal(new[] { "03/06", "04/06" }, trend.Labels);
        Assert.Equal(2, trend.Datasets.Count);
        // Gross series: evening 20:00 stays on 03/06, not spilled to 04/06.
        Assert.Equal(100m, trend.Datasets[0].Data[0]);
        Assert.Equal(50m, trend.Datasets[0].Data[1]);
    }

    [Fact]
    public async Task GetHubAsync_Cancellations_CustomSingleDay_IncludesEveningOrders()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        ctx.Orders.AddRange(
            new Order
            {
                Id = 10,
                OrderNumber = "ORD-EVE",
                CreatedAt = new DateTime(2026, 6, 3, 20, 0, 0),
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.Pending,
                Total = 80m,
                Subtotal = 80m
            },
            new Order
            {
                Id = 11,
                OrderNumber = "ORD-EAR",
                CreatedAt = new DateTime(2026, 6, 3, 2, 0, 0),
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.Pending,
                Total = 40m,
                Subtotal = 40m
            },
            new Order
            {
                Id = 12,
                OrderNumber = "ORD-NXT",
                CreatedAt = new DateTime(2026, 6, 4, 1, 0, 0),
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                Total = 10m,
                Subtotal = 10m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 6, 3),
            To = new DateTime(2026, 6, 3),
            Tab = SalesAnalyticsTab.Cancellations
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.Null(hub.Error);
        Assert.NotNull(hub.Cancellations);
        Assert.Equal(2, hub.Cancellations!.CancelledCount.Value);
        Assert.Equal(120m, hub.Cancellations.CancelledValue.Value);
        Assert.Equal(100m, hub.Cancellations.CancelRate.Value); // 2/2 in range cancelled
    }

    [Fact]
    public async Task GetHubAsync_Cancellations_GroupsNullEmptyWhitespaceAsUnknownReason()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var ctx = new ApplicationDbContext(options);

        var day = new DateTime(2026, 6, 3, 12, 0, 0);
        ctx.Orders.AddRange(
            new Order
            {
                Id = 1,
                OrderNumber = "ORD-1",
                Status = OrderStatus.Cancelled,
                CancelReason = null,
                CreatedAt = day,
                Total = 10m,
                Subtotal = 10m
            },
            new Order
            {
                Id = 2,
                OrderNumber = "ORD-2",
                Status = OrderStatus.Cancelled,
                CancelReason = "",
                CreatedAt = day,
                Total = 10m,
                Subtotal = 10m
            },
            new Order
            {
                Id = 3,
                OrderNumber = "ORD-3",
                Status = OrderStatus.Cancelled,
                CancelReason = "   ",
                CreatedAt = day,
                Total = 10m,
                Subtotal = 10m
            },
            new Order
            {
                Id = 4,
                OrderNumber = "ORD-4",
                Status = OrderStatus.Cancelled,
                CancelReason = "Khách hàng đổi ý",
                CreatedAt = day,
                Total = 10m,
                Subtotal = 10m
            },
            new Order
            {
                Id = 5,
                OrderNumber = "ORD-5",
                Status = OrderStatus.Cancelled,
                CancelReason = "Khách hàng đổi ý",
                CreatedAt = day,
                Total = 10m,
                Subtotal = 10m
            },
            new Order
            {
                Id = 6,
                OrderNumber = "ORD-6",
                Status = OrderStatus.Pending,
                CancelReason = null,
                CreatedAt = day,
                Total = 10m,
                Subtotal = 10m
            });
        await ctx.SaveChangesAsync();

        var filter = new SalesAnalyticsFilterVm
        {
            Preset = DateRangePreset.Custom,
            From = new DateTime(2026, 6, 3),
            To = new DateTime(2026, 6, 3),
            Tab = SalesAnalyticsTab.Cancellations
        };

        var hub = await new SalesAnalyticsService(new UnitOfWork(ctx)).GetHubAsync(filter);

        Assert.NotNull(hub.Cancellations);
        var reasons = hub.Cancellations!.Reasons;
        Assert.Equal(5, hub.Cancellations.CancelledCount.Value);
        Assert.Contains("Không ghi rõ", reasons.Labels);
        Assert.Contains("Khách hàng đổi ý", reasons.Labels);

        var unknownIdx = reasons.Labels.IndexOf("Không ghi rõ");
        var doiYIdx = reasons.Labels.IndexOf("Khách hàng đổi ý");
        Assert.Equal(3m, reasons.Datasets[0].Data[unknownIdx]); // null + empty + whitespace
        Assert.Equal(2m, reasons.Datasets[0].Data[doiYIdx]);
    }
}

