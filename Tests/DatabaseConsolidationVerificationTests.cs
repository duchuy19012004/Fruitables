using System.Text.Json;
using Fruitables.Data;
using Fruitables.Services.Infrastructure.DatabaseConsolidation;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fruitables.Tests;

public class DatabaseConsolidationVerificationTests
{
    [Fact]
    public async Task Verify_reports_source_target_counts_and_json_clean_after_backfill()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);

        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        var report = await service.VerifyAsync(CancellationToken.None);

        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));
        Assert.True(report.Success, string.Join(Environment.NewLine, report.Errors.Select(error => error.Message)));
        Assert.True(report.IsJsonValid);
        Assert.Equal(1, report.SourceCounts["payments"]);
        Assert.Equal(1, report.TargetCounts["payments"]);
        Assert.Equal(3, report.SourceCounts["promotions"]);
        Assert.Equal(3, report.TargetCounts["promotions"]);
        Assert.Equal(1, report.SourceCounts["returns"]);
        Assert.Equal(1, report.TargetCounts["returns"]);
        Assert.Empty(report.FailedSourceIds);
    }

    [Fact]
    public async Task Verify_detects_json_count_total_quantity_stock_payment_review_and_return_mismatches()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        var product = await db.Products.SingleAsync(product => product.Id == DatabaseConsolidationFixture.ProductId);
        product.ImagesJson = "not-json";
        product.ReviewCount = 99;
        var order = await db.Orders.SingleAsync(order => order.Id == DatabaseConsolidationFixture.OrderId);
        order.Total = 999m;
        var variant = await db.ProductVariants.SingleAsync(variant => variant.Id == DatabaseConsolidationFixture.VariantId);
        variant.StockQuantity = -1m;
        var payment = await db.Payments.SingleAsync();
        payment.ProviderTransactionId = "wrong-transaction";
        var returnCase = await db.Returns.SingleAsync();
        returnCase.ApprovedAmount = 999m;
        await db.SaveChangesAsync();

        var report = await service.VerifyAsync(CancellationToken.None);
        Console.WriteLine(string.Join(" | ", report.Errors.Select(error => $"{error.SourceId}:{error.Message}")));

        Assert.False(report.Success);
        Assert.False(report.IsJsonValid);
        Assert.Contains(report.Errors, error => error.Message.Contains("ISJSON", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("order total", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("stock", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("payment transaction", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("review count", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("approved return", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.FailedSourceIds, sourceId => sourceId == "Product:10");
        Assert.Contains(report.FailedSourceIds, sourceId => sourceId == "Order:50");
        Assert.Contains(report.FailedSourceIds, sourceId => sourceId == "ProductVariant:102");
        Assert.Contains(report.FailedSourceIds, sourceId => sourceId == "Payment:SePay:12345");
        Assert.Contains(report.FailedSourceIds, sourceId => sourceId == "Return:50");
    }

    [Fact]
    public async Task Verify_does_not_write_business_data()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        var before = await DatabaseConsolidationFixture.SnapshotAsync(db);
        var productJson = (await db.Products.SingleAsync(product => product.Id == DatabaseConsolidationFixture.ProductId)).ImagesJson;
        var report = await service.VerifyAsync(CancellationToken.None);
        var after = await DatabaseConsolidationFixture.SnapshotAsync(db);
        var productJsonAfter = (await db.Products.SingleAsync(product => product.Id == DatabaseConsolidationFixture.ProductId)).ImagesJson;

        Assert.True(report.Success);
        Assert.Equal(before, after);
        Assert.Equal(productJson, productJsonAfter);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Fact]
    public async Task Verify_empty_database_is_clean()
    {
        var options = TestDbContextFactory.CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);
        var report = await CreateService(db).VerifyAsync(CancellationToken.None);

        Assert.True(report.Success, string.Join(Environment.NewLine, report.Errors.Select(error => error.Message)));
        Assert.True(report.IsJsonValid);
        Assert.Empty(report.FailedSourceIds);
    }

    private static IDatabaseConsolidationService CreateService(ApplicationDbContext db) =>
        new DatabaseConsolidationService(
            db,
            new VersionedJsonSerializer(),
            NullLogger<DatabaseConsolidationService>.Instance);
}
