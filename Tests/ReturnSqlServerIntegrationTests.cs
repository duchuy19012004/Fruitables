using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Outbox;
using Fruitables.Services.Returns;
using Fruitables.ViewModels.Returns;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FRUITABLES_TEST_SQLSERVER")))
            Skip = "Set FRUITABLES_TEST_SQLSERVER to run SQL Server integration tests.";
    }
}

public class ReturnSqlServerIntegrationTests
{
    [SqlServerFact]
    public async Task ConcurrentSubmitsCannotClaimMoreThanOrderedQuantity()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database.Options, quantity: 1);
        await using var db1 = new ApplicationDbContext(database.Options);
        await using var db2 = new ApplicationDbContext(database.Options);
        var task1 = Returns(db1).SubmitAsync(seed.UserId, Submit(seed, "concurrent-a"));
        var task2 = Returns(db2).SubmitAsync(seed.UserId, Submit(seed, "concurrent-b"));
        var results = await Task.WhenAll(task1, task2);
        Assert.Single(results.Where(x => x.Success));
        await using var verify = new ApplicationDbContext(database.Options);
        var claimed = await verify.ReturnRequestItems.Where(x => x.OrderItemId == seed.OrderItemId).SumAsync(x => x.RequestedQuantity);
        Assert.Equal(1, claimed);
    }

    [SqlServerFact]
    public async Task ConcurrentAdminDecisionsProduceOneConcurrencyConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database.Options, quantity: 2, createReturn: true);
        await using var read = new ApplicationDbContext(database.Options);
        var snapshot = await read.ReturnRequests.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == seed.ReturnRequestId);
        var encoded = Convert.ToBase64String(snapshot.RowVersion!);
        var itemId = snapshot.Items.Single().Id;
        ReturnDecisionViewModel Decision(string reason) => new() { ReturnRequestId = snapshot.Id, RowVersion = encoded, Reason = reason, Items = { new ReturnDecisionItemViewModel { ReturnRequestItemId = itemId, ApprovedQuantity = 1 } } };
        await using var db1 = new ApplicationDbContext(database.Options);
        await using var db2 = new ApplicationDbContext(database.Options);
        var results = await Task.WhenAll(Returns(db1).DecideAsync(1, Decision("Duyệt bởi nhân viên A")), Returns(db2).DecideAsync(2, Decision("Duyệt bởi nhân viên B")));
        Assert.Single(results.Where(x => x.Success));
        Assert.Single(results.Where(x => x.IsConcurrencyConflict));
        await using var verify = new ApplicationDbContext(database.Options);
        Assert.Single(await verify.Refunds
            .Where(x => x.ReturnRequestId == snapshot.Id && x.ReturnRequestItemId == null)
            .ToListAsync());
    }

    [SqlServerFact]
    public async Task RolledBackTransactionCannotPublishOutboxMessage()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var db = new ApplicationDbContext(database.Options))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await new OutboxService(db, TimeProvider.System).EnqueueAsync("returns.test", new { returnRequestId = 42 }, "rollback-notification");
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        await using var verify = new ApplicationDbContext(database.Options);
        var claimed = await new OutboxService(verify, TimeProvider.System).ClaimAsync(10, "verification-worker", TimeSpan.FromMinutes(1));
        Assert.Empty(claimed);
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }

    [SqlServerFact]
    public async Task ConcurrentOutboxWorkersClaimDisjointMessages()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var seedDb = new ApplicationDbContext(database.Options))
        {
            var outbox = new OutboxService(seedDb, TimeProvider.System);
            for (var i = 0; i < 10; i++) await outbox.EnqueueAsync("returns.test", new { index = i }, $"claim-{i}");
            await seedDb.SaveChangesAsync();
        }
        await using var db1 = new ApplicationDbContext(database.Options);
        await using var db2 = new ApplicationDbContext(database.Options);
        var claims = await Task.WhenAll(
            new OutboxService(db1, TimeProvider.System).ClaimAsync(5, "worker-a", TimeSpan.FromMinutes(1)),
            new OutboxService(db2, TimeProvider.System).ClaimAsync(5, "worker-b", TimeSpan.FromMinutes(1)));
        Assert.Equal(10, claims.SelectMany(x => x).Select(x => x.Id).Distinct().Count());
        Assert.Empty(claims[0].Select(x => x.Id).Intersect(claims[1].Select(x => x.Id)));
    }

    [SqlServerFact]
    public async Task DuplicateRefundReferenceIsRejectedByUniqueConstraint()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database.Options, quantity: 2, createReturn: true);
        await using (var first = new ApplicationDbContext(database.Options))
        {
            first.Refunds.Add(Refund(seed, "refund-a", "BANK-DUPLICATE"));
            await first.SaveChangesAsync();
        }
        await using var second = new ApplicationDbContext(database.Options);
        second.Refunds.Add(Refund(seed, "refund-b", "BANK-DUPLICATE"));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    private static ReturnService Returns(ApplicationDbContext db) => new(db, new ReturnEligibilityService(db, new ReturnPolicyService(db), TimeProvider.System), new RefundAmountCalculator(db), TimeProvider.System);
    private static ReturnSubmitViewModel Submit(Seed seed, string key) => new() { OrderId = seed.OrderId, IdempotencyKey = key, Items = { new ReturnSubmitItemViewModel { Selected = true, OrderItemId = seed.OrderItemId, Quantity = 1, Reason = ReturnReasonCode.Other, Description = "Sản phẩm không đạt chất lượng" } } };
    private static Refund Refund(Seed seed, string key, string reference) => new() { ReturnRequestId = seed.ReturnRequestId, ReturnRequestItemId = seed.ReturnRequestItemId, OrderId = seed.OrderId, Amount = 10, Method = RefundMethod.ManualBankTransfer, Status = RefundStatus.Succeeded, IdempotencyKey = key, TransactionReference = reference, CreatedByUserId = 1, ProcessedByUserId = 2, CreatedAtUtc = DateTime.UtcNow, ProcessedAtUtc = DateTime.UtcNow };

    private static async Task<Seed> SeedAsync(DbContextOptions<ApplicationDbContext> options, int quantity, bool createReturn = false)
    {
        await using var db = new ApplicationDbContext(options);
        var customer = new User { Name = "SQL Customer", Email = $"sql-{Guid.NewGuid():N}@test.local", Password = "hash" };
        var category = new Category { Name = "SQL Fruit", Slug = $"sql-fruit-{Guid.NewGuid():N}" };
        var product = new Product { Category = category, Name = "SQL Apple", Slug = $"sql-apple-{Guid.NewGuid():N}", Price = 10, StockQuantity = 10 };
        var order = new Order { User = customer, OrderNumber = $"SQL-{Guid.NewGuid():N}", Status = OrderStatus.Delivered, PaymentStatus = PaymentStatus.Paid, DeliveredAtUtc = DateTime.UtcNow.AddHours(-1), Subtotal = 10 * quantity, Total = 10 * quantity };
        var orderItem = new OrderItem { Order = order, Product = product, ProductName = product.Name, Quantity = quantity, BasePrice = 10, Price = 10, Total = 10 * quantity };
        order.Items.Add(orderItem);
        var policy = new ReturnPolicy { Name = "SQL Default", Scope = ReturnPolicyScope.Default, Reason = ReturnReasonCode.Other, ClaimWindowHours = 24, AllowPartialRefund = true, AllowFullRefund = true, AllowReplacement = true, AllowStoreCredit = true, IsEligible = true, IsActive = true, Version = 99, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1), CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(order, policy);
        await db.SaveChangesAsync();
        ReturnRequest? request = null;
        if (createReturn)
        {
            request = new ReturnRequest { ReturnNumber = $"RT{Guid.NewGuid():N}"[..24], IdempotencyKey = Guid.NewGuid().ToString("N"), OrderId = order.Id, UserId = customer.Id, Status = ReturnRequestStatus.UnderReview, SubmittedAtUtc = DateTime.UtcNow, ClaimDeadlineAtUtc = DateTime.UtcNow.AddHours(23), ReviewDueAtUtc = DateTime.UtcNow.AddHours(24), Items = { new ReturnRequestItem { OrderItemId = orderItem.Id, ReturnPolicyId = policy.Id, RequestedQuantity = quantity, Reason = ReturnReasonCode.Other, Description = "SQL quality issue", NetPaidAmountSnapshot = 10 * quantity, RequestedAmount = 10 * quantity, PolicyVersionSnapshot = policy.Version, ClaimWindowHoursSnapshot = 24, ClaimDeadlineAtUtcSnapshot = DateTime.UtcNow.AddHours(23) } } };
            db.ReturnRequests.Add(request);
            await db.SaveChangesAsync();
        }
        return new(customer.Id, order.Id, orderItem.Id, request?.Id ?? 0, request?.Items.Single().Id ?? 0);
    }

    private sealed record Seed(int UserId, int OrderId, int OrderItemId, int ReturnRequestId, int ReturnRequestItemId);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;
        public DbContextOptions<ApplicationDbContext> Options { get; }
        private TestDatabase(string connectionString) { _connectionString = connectionString; Options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("FRUITABLES_TEST_SQLSERVER")!;
            var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"Fruitables_ReturnTests_{Guid.NewGuid():N}" };
            var database = new TestDatabase(builder.ConnectionString);
            await using var db = new ApplicationDbContext(database.Options);
            await db.Database.MigrateAsync();
            return database;
        }
        public async ValueTask DisposeAsync()
        {
            await using var db = new ApplicationDbContext(Options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
