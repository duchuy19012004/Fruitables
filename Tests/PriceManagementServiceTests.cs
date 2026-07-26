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
            Targets = [new(1, null), new(2, null)],
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

        var result = await CreateService(context).UpdateBasePriceAsync(new PriceTargetKey(1, null), 80_000, 7);

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
