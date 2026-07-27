using Fruitables.Data;
using Fruitables.Services.Outbox;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public class OutboxServiceTests
{
    [Fact]
    public async Task EnqueueJoinsCallerSaveAndUsesStableIdempotencyKey()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        var clock = new MutableTimeProvider(Utc(2026, 7, 27));
        var service = new OutboxService(db, clock);
        var first = await service.EnqueueAsync("returns.test", new { returnRequestId = 12 }, "return:12:test");
        var duplicate = await service.EnqueueAsync("returns.test", new { returnRequestId = 12 }, "return:12:test");
        Assert.Same(first, duplicate);
        await using (var beforeSave = new ApplicationDbContext(options)) Assert.Empty(await beforeSave.OutboxMessages.ToListAsync());
        await db.SaveChangesAsync();
        await using var afterSave = new ApplicationDbContext(options);
        Assert.Single(await afterSave.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task CompletedMessageCannotBeClaimedOrConsumedAgain()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        var clock = new MutableTimeProvider(Utc(2026, 7, 27));
        await SeedAsync(options, clock, "once");
        await using var db = new ApplicationDbContext(options);
        var service = new OutboxService(db, clock);
        var claimed = await service.ClaimAsync(10, "worker-a", TimeSpan.FromMinutes(1));
        var message = Assert.Single(claimed);
        Assert.Equal(1, message.AttemptCount);
        Assert.True(await service.CompleteAsync(message.Id, "worker-a"));
        Assert.Empty(await service.ClaimAsync(10, "worker-b", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task FailureUsesExponentialBackoffThenDeadLettersAtLimit()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        var clock = new MutableTimeProvider(Utc(2026, 7, 27));
        await SeedAsync(options, clock, "retry");
        await using var db = new ApplicationDbContext(options);
        var service = new OutboxService(db, clock);

        var first = Assert.Single(await service.ClaimAsync(1, "worker", TimeSpan.FromMinutes(1)));
        Assert.True(await service.FailAsync(first.Id, "worker", new IOException("temporary"), 3, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1)));
        Assert.Equal(clock.UtcNow.AddSeconds(5), first.NextAttemptAtUtc);
        Assert.Empty(await service.ClaimAsync(1, "worker", TimeSpan.FromMinutes(1)));

        clock.UtcNow = first.NextAttemptAtUtc;
        var second = Assert.Single(await service.ClaimAsync(1, "worker", TimeSpan.FromMinutes(1)));
        await service.FailAsync(second.Id, "worker", new IOException("temporary"), 3, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
        Assert.Equal(clock.UtcNow.AddSeconds(10), second.NextAttemptAtUtc);

        clock.UtcNow = second.NextAttemptAtUtc;
        var third = Assert.Single(await service.ClaimAsync(1, "worker", TimeSpan.FromMinutes(1)));
        await service.FailAsync(third.Id, "worker", new InvalidOperationException("permanent"), 3, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
        Assert.Equal(clock.UtcNow, third.DeadLetteredAtUtc);
        Assert.Empty(await service.ClaimAsync(1, "worker", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task CleanupRemovesOnlyExpiredProcessedAndDeadLetterMessages()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        var clock = new MutableTimeProvider(Utc(2026, 7, 27));
        await using var db = new ApplicationDbContext(options);
        db.OutboxMessages.AddRange(
            Message("old-processed", clock.UtcNow.AddDays(-40), processed: clock.UtcNow.AddDays(-31)),
            Message("recent-processed", clock.UtcNow.AddDays(-2), processed: clock.UtcNow.AddDays(-1)),
            Message("old-dead", clock.UtcNow.AddDays(-100), deadLettered: clock.UtcNow.AddDays(-91)),
            Message("pending", clock.UtcNow));
        await db.SaveChangesAsync();
        var deleted = await new OutboxService(db, clock).CleanupAsync(TimeSpan.FromDays(30), TimeSpan.FromDays(90));
        Assert.Equal(2, deleted);
        Assert.Equal(new[] { "pending", "recent-processed" }, await db.OutboxMessages.OrderBy(x => x.IdempotencyKey).Select(x => x.IdempotencyKey).ToArrayAsync());
    }

    private static async Task SeedAsync(DbContextOptions<ApplicationDbContext> options, TimeProvider clock, string key)
    {
        await using var db = new ApplicationDbContext(options);
        var service = new OutboxService(db, clock);
        await service.EnqueueAsync("returns.test", new { key }, key);
        await db.SaveChangesAsync();
    }

    private static Fruitables.Models.OutboxMessage Message(string key, DateTime occurred, DateTime? processed = null, DateTime? deadLettered = null) => new()
    {
        Type = "returns.test", Payload = "{}", IdempotencyKey = key, OccurredAtUtc = occurred,
        NextAttemptAtUtc = occurred, ProcessedAtUtc = processed, DeadLetteredAtUtc = deadLettered
    };
    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    private sealed class MutableTimeProvider(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }
}
