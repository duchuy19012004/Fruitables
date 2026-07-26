using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public class PriceManagementServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateSchedule_rejects_zero_fixed_price()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateScheduleAsync(new SavePriceScheduleRequest
        {
            ProductId = 1,
            DiscountType = DiscountType.FixedPrice,
            Value = 0,
            StartsAt = Now.AddHours(1),
            EndsAt = Now.AddHours(2)
        }, 7);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateSchedule_rejects_one_hundred_percent_discount()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateScheduleAsync(new SavePriceScheduleRequest
        {
            ProductId = 1,
            DiscountType = DiscountType.Percentage,
            Value = 100,
            StartsAt = Now.AddHours(1),
            EndsAt = Now.AddHours(2)
        }, 7);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateSchedule_rejects_schedule_already_ended()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateScheduleAsync(new SavePriceScheduleRequest
        {
            ProductId = 1,
            DiscountType = DiscountType.Percentage,
            Value = 10,
            StartsAt = Now.AddHours(-2),
            EndsAt = Now.AddHours(-1)
        }, 7);

        Assert.False(result.Success);
        Assert.Contains("đã kết thúc", result.Error);
    }

    [Fact]
    public async Task UpdateBasePrice_rejects_stale_price_snapshot()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 1,
            Name = "Táo",
            Slug = "tao",
            Price = 120_000,
            PriceRevision = 2
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateBasePriceAsync(new UpdateBasePriceRequest
        {
            ProductId = 1,
            NewPrice = 90_000,
            ExpectedBasePrice = 100_000,
            ExpectedRevision = 1
        }, 7);

        Assert.False(result.Success);
        Assert.Contains("đã thay đổi", result.Error);
        Assert.Equal(120_000, context.Products.Find(1)!.Price);
        Assert.Equal(2, context.Products.Find(1)!.PriceRevision);
    }

    [Fact]
    public async Task BulkUpdate_when_one_target_is_stale_updates_nothing()
    {
        await using var context = CreateContext();
        context.Products.AddRange(
            new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000, PriceRevision = 1 },
            new Product { Id = 2, Name = "Cam", Slug = "cam", Price = 120_000, PriceRevision = 2 });
        await context.SaveChangesAsync();

        var result = await CreateService(context).BulkUpdateBasePricesAsync(new BulkPriceUpdateRequest
        {
            AdjustmentType = PriceAdjustmentType.Percentage,
            Direction = PriceAdjustmentDirection.Decrease,
            Value = 10,
            Targets =
            [
                new BulkPriceTargetRequest { ProductId = 1, ExpectedBasePrice = 100_000, ExpectedRevision = 1 },
                new BulkPriceTargetRequest { ProductId = 2, ExpectedBasePrice = 100_000, ExpectedRevision = 1 }
            ]
        }, 7);

        Assert.False(result.Success);
        Assert.Equal(100_000, context.Products.Find(1)!.Price);
        Assert.Equal(120_000, context.Products.Find(2)!.Price);
    }

    [Fact]
    public async Task CreateSchedule_rejects_an_overlapping_schedule_for_the_same_target()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        context.PriceSchedules.Add(new PriceSchedule
        {
            ProductId = 1, DiscountType = DiscountType.Percentage, Value = 10,
            StartsAt = Now.AddHours(1), EndsAt = Now.AddHours(3)
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.CreateScheduleAsync(new SavePriceScheduleRequest
        {
            ProductId = 1, DiscountType = DiscountType.FixedPrice, Value = 80_000,
            StartsAt = Now.AddHours(2), EndsAt = Now.AddHours(4)
        }, adminId: 7);

        Assert.False(result.Success);
        Assert.Contains("trùng", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(context.PriceSchedules);
    }

    [Fact]
    public async Task BulkUpdate_is_all_or_nothing_when_a_fixed_schedule_would_be_invalid()
    {
        await using var context = CreateContext();
        context.Products.AddRange(
            new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 },
            new Product { Id = 2, Name = "Cam", Slug = "cam", Price = 120_000 });
        context.PriceSchedules.Add(new PriceSchedule
        {
            ProductId = 2, DiscountType = DiscountType.FixedPrice, Value = 110_000,
            StartsAt = Now.AddHours(-1), EndsAt = null
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.BulkUpdateBasePricesAsync(new BulkPriceUpdateRequest
        {
            Targets =
            [
                new BulkPriceTargetRequest { ProductId = 1, ExpectedBasePrice = 100_000, ExpectedRevision = 1 },
                new BulkPriceTargetRequest { ProductId = 2, ExpectedBasePrice = 120_000, ExpectedRevision = 1 }
            ],
            AdjustmentType = PriceAdjustmentType.Amount,
            Direction = PriceAdjustmentDirection.Decrease,
            Value = 20_000
        }, adminId: 7);

        Assert.False(result.Success);
        Assert.Equal(100_000, context.Products.Find(1)!.Price);
        Assert.Equal(120_000, context.Products.Find(2)!.Price);
    }

    [Fact]
    public async Task Product_level_schedule_is_rejected_when_active_variants_exist()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        context.ProductVariants.Add(new ProductVariant
        {
            Id = 4, ProductId = 1, Name = "Hộp 1kg", SKU = "TAO-1", Price = 100_000, IsActive = true
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateScheduleAsync(new SavePriceScheduleRequest
        {
            ProductId = 1, DiscountType = DiscountType.Percentage, Value = 10,
            StartsAt = Now.AddHours(1), EndsAt = Now.AddHours(2)
        }, 7);

        Assert.False(result.Success);
        Assert.Contains("từng biến thể", result.Error);
    }

    [Fact]
    public async Task Base_price_change_is_rejected_if_a_future_fixed_price_would_be_invalid()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        context.PriceSchedules.Add(new PriceSchedule
        {
            ProductId = 1, DiscountType = DiscountType.FixedPrice, Value = 90_000,
            StartsAt = Now.AddHours(1), EndsAt = null
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateBasePriceAsync(new UpdateBasePriceRequest
        {
            ProductId = 1,
            NewPrice = 80_000,
            ExpectedBasePrice = 100_000,
            ExpectedRevision = 1
        }, 7);

        Assert.False(result.Success);
        Assert.Equal(100_000, context.Products.Find(1)!.Price);
    }

    [Fact]
    public async Task UpdateSchedule_rejects_stale_revision_without_mutating_schedule()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        context.PriceSchedules.Add(new PriceSchedule
        {
            Id = 4,
            ProductId = 1,
            DiscountType = DiscountType.Percentage,
            Value = 10,
            StartsAt = Now.AddHours(2),
            EndsAt = Now.AddHours(4),
            Revision = 3
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateScheduleAsync(4, new SavePriceScheduleRequest
        {
            ProductId = 1,
            DiscountType = DiscountType.Percentage,
            Value = 20,
            StartsAt = Now.AddHours(2),
            EndsAt = Now.AddHours(4),
            ExpectedRevision = 2
        }, 7);

        Assert.False(result.Success);
        Assert.Contains("đã thay đổi", result.Error);
        Assert.Equal(10, context.PriceSchedules.Find(4)!.Value);
        Assert.Equal(3, context.PriceSchedules.Find(4)!.Revision);
    }

    [Fact]
    public async Task Cancel_active_schedule_records_stopped_early_metadata_and_reason()
    {
        await using var context = CreateContext();
        context.Users.Add(new User { Id = 7, Name = "Admin", Email = "admin@example.com", Password = "x" });
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        context.PriceSchedules.Add(new PriceSchedule
        {
            Id = 5,
            ProductId = 1,
            DiscountType = DiscountType.Percentage,
            Value = 10,
            StartsAt = Now.AddHours(-1),
            EndsAt = Now.AddHours(2),
            Revision = 4
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CancelScheduleAsync(5, new CancelPriceScheduleRequest
        {
            ExpectedRevision = 4,
            Reason = "Dừng sớm do sai giá nhập"
        }, 7);

        var schedule = context.PriceSchedules.Find(5)!;
        Assert.True(result.Success);
        Assert.True(schedule.IsCancelled);
        Assert.Equal(Now, schedule.CancelledAt);
        Assert.Equal(7, schedule.CancelledByAdminId);
        Assert.Equal("Dừng sớm do sai giá nhập", schedule.CancellationReason);
        Assert.Equal(5, schedule.Revision);
        Assert.Equal(PriceScheduleStatus.StoppedEarly, schedule.GetStatus(Now));
    }

    [Fact]
    public async Task Active_schedule_can_be_cancelled_and_is_kept_as_history()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        context.PriceSchedules.Add(new PriceSchedule
        {
            Id = 3, ProductId = 1, DiscountType = DiscountType.Percentage, Value = 10,
            StartsAt = Now.AddHours(-1), EndsAt = null
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CancelScheduleAsync(3, new CancelPriceScheduleRequest
        {
            ExpectedRevision = 1
        }, 7);

        Assert.True(result.Success);
        Assert.True(context.PriceSchedules.Find(3)!.IsCancelled);
        Assert.Equal(PriceScheduleStatus.StoppedEarly, context.PriceSchedules.Find(3)!.GetStatus(Now));
        Assert.Contains(context.ProductLogs, log => log.Action == "PriceScheduleStoppedEarly");
    }

    [Fact]
    public void GetStatus_cancelled_before_start_is_cancelled()
    {
        var schedule = new PriceSchedule
        {
            StartsAt = Now.AddHours(2),
            IsCancelled = true,
            CancelledAt = Now
        };

        Assert.Equal(PriceScheduleStatus.Cancelled, schedule.GetStatus(Now));
    }

    [Fact]
    public void GetStatus_cancelled_after_start_is_stopped_early()
    {
        var schedule = new PriceSchedule
        {
            StartsAt = Now.AddHours(-2),
            EndsAt = Now.AddHours(2),
            IsCancelled = true,
            CancelledAt = Now
        };

        Assert.Equal(PriceScheduleStatus.StoppedEarly, schedule.GetStatus(Now));
    }

    [Fact]
    public async Task CreateSchedule_rejects_an_unknown_discount_type()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 1, Name = "Táo", Slug = "tao", Price = 100_000 });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateScheduleAsync(new SavePriceScheduleRequest
        {
            ProductId = 1, DiscountType = (DiscountType)999, Value = -1,
            StartsAt = Now.AddHours(1), EndsAt = Now.AddHours(2)
        }, 7);

        Assert.False(result.Success);
        Assert.Contains("không hợp lệ", result.Error);
    }

    [Fact]
    public async Task UpdateBasePrice_rejects_fractional_vnd()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 1,
            Name = "Táo",
            Slug = "tao-fractional-base",
            Price = 100_000,
            PriceRevision = 1
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateBasePriceAsync(
            new UpdateBasePriceRequest
            {
                ProductId = 1,
                NewPrice = 100_000.5m,
                ExpectedBasePrice = 100_000,
                ExpectedRevision = 1
            },
            adminId: 7);

        Assert.False(result.Success);
        Assert.Contains("số nguyên", result.Error);
        Assert.Equal(100_000, context.Products.Find(1)!.Price);
    }

    [Fact]
    public async Task CreateSchedule_rejects_fractional_fixed_vnd()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 1,
            Name = "Táo",
            Slug = "tao-fractional-schedule",
            Price = 100_000
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateScheduleAsync(
            new SavePriceScheduleRequest
            {
                ProductId = 1,
                DiscountType = DiscountType.FixedPrice,
                Value = 80_000.5m,
                StartsAt = Now.AddHours(1),
                EndsAt = Now.AddHours(2)
            },
            adminId: 7);

        Assert.False(result.Success);
        Assert.Contains("số nguyên", result.Error);
        Assert.Empty(context.PriceSchedules);
    }

    [Fact]
    public async Task Bulk_fixed_amount_rejects_fractional_vnd_and_updates_nothing()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 1,
            Name = "Táo",
            Slug = "tao-fractional-bulk",
            Price = 100_000,
            PriceRevision = 1
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).BulkUpdateBasePricesAsync(
            new BulkPriceUpdateRequest
            {
                AdjustmentType = PriceAdjustmentType.Amount,
                Direction = PriceAdjustmentDirection.Increase,
                Value = 1_000.5m,
                Targets =
                {
                    new BulkPriceTargetRequest
                    {
                        ProductId = 1,
                        ExpectedBasePrice = 100_000,
                        ExpectedRevision = 1
                    }
                }
            },
            adminId: 7);

        Assert.False(result.Success);
        Assert.Contains("số nguyên", result.Error);
        Assert.Equal(100_000, context.Products.Find(1)!.Price);
    }

    [Fact]
    public async Task CreateSchedule_accepts_decimal_percentage_inside_range()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 1,
            Name = "Táo",
            Slug = "tao-decimal-percent",
            Price = 100_000
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateScheduleAsync(
            new SavePriceScheduleRequest
            {
                ProductId = 1,
                DiscountType = DiscountType.Percentage,
                Value = 10.5m,
                StartsAt = Now.AddHours(1),
                EndsAt = Now.AddHours(2)
            },
            adminId: 7);

        Assert.True(result.Success);
        Assert.Equal(10.5m, context.PriceSchedules.Single().Value);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static PriceManagementService CreateService(ApplicationDbContext context) =>
        new(new UnitOfWork(context), new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
