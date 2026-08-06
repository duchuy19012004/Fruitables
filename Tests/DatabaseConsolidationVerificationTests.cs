using System.Text.Json;
using Fruitables.Areas.Admin.Controllers;
using Fruitables.Attributes;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Services.Infrastructure.DatabaseConsolidation;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
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
    public async Task Verify_reports_a_product_variant_that_has_no_product_parent()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        db.ProductVariants.Add(new ProductVariant
        {
            Id = 999,
            ProductId = 4040,
            SKU = "ORPHAN-1",
            Name = "Orphan",
            Price = 1m,
            StockQuantity = 1m
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        var report = await service.VerifyAsync(CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error =>
            error.SourceId == "ProductVariant:999"
            && error.Message.Contains("product", StringComparison.OrdinalIgnoreCase));
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
    public async Task Verify_rejects_valid_json_with_an_invalid_typed_promotion_payload()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        var promotion = await db.Promotions.SingleAsync(item => item.Type == "combo");
        promotion.PayloadJson = "{\"schemaVersion\":1}";
        await db.SaveChangesAsync();

        var report = await service.VerifyAsync(CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error =>
            error.AggregateType == "Promotion"
            && error.Message.Contains("Typed JSON validation failed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("coupon")]
    [InlineData("price-schedule")]
    public async Task Verify_rejects_valid_json_with_invalid_coupon_and_price_schedule_payloads(string type)
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        var promotion = await db.Promotions.SingleAsync(item => item.Type == type);
        promotion.PayloadJson = "{\"schemaVersion\":1}";
        await db.SaveChangesAsync();

        var report = await service.VerifyAsync(CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error =>
            error.AggregateType == "Promotion"
            && error.SourceId == $"Promotion:{promotion.Type}:{promotion.Code}"
            && error.Message.Contains("Typed JSON validation failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Verify_reports_extra_target_rows_separately_from_source_derived_keys()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        db.Promotions.Add(new Promotion
        {
            Type = "manual",
            Code = "manual:extra",
            PayloadJson = "{\"schemaVersion\":1}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var report = await service.VerifyAsync(CancellationToken.None);

        Assert.False(report.Success);
        Assert.Equal(1, report.TargetCounts["promotions.extra"]);
        Assert.Contains(report.Errors, error => error.SourceId.Contains("extra", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Verify_compares_payment_amount_event_status_and_refund_values()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        var payment = await db.Payments.SingleAsync();
        payment.Amount = 999m;
        payment.ProviderEventStatus = PaymentProviderEventStatus.Ignored;
        var refund = await db.Refunds.SingleAsync();
        refund.Amount = 999m;
        await db.SaveChangesAsync();

        var report = await service.VerifyAsync(CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error => error.Message.Contains("payment amount", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("provider event status", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("refund amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Verify_recomputes_order_item_arithmetic_and_order_summary()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var backfill = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(backfill.Success, string.Join(Environment.NewLine, backfill.Errors.Select(error => error.Message)));

        var item = await db.OrderItems.SingleAsync(orderItem => orderItem.OrderId == DatabaseConsolidationFixture.OrderId);
        item.Quantity = 3m;
        item.PromotionDiscount = 1m;
        var order = await db.Orders.SingleAsync(orderItem => orderItem.Id == DatabaseConsolidationFixture.OrderId);
        order.Subtotal = 20m;
        order.Total = 21m;
        await db.SaveChangesAsync();

        var report = await service.VerifyAsync(CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error => error.Message.Contains("order item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("promotion discount", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, error => error.Message.Contains("subtotal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Diagnostics_status_requires_permission_and_is_not_anonymous()
    {
        var method = typeof(DiagnosticsController).GetMethod(nameof(DiagnosticsController.DatabaseConsolidationStatus));
        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true), attribute =>
            ((RequirePermissionAttribute)attribute).Permissions.Contains("system.manage_rbac"));
        Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void Database_consolidation_cli_maps_report_success_to_exit_code(bool success, int expectedExitCode)
    {
        Assert.Equal(expectedExitCode, DatabaseConsolidationCli.ExitCode(success));
    }

    [Fact]
    public void Sql_server_json_guard_emits_actual_isjson_predicates()
    {
        var sql = DatabaseConsolidationSql.BuildIsJsonQuery(
            "Products",
            ["ImagesJson", "TagsJson"]);

        Assert.Contains("ISJSON([ImagesJson])", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ISJSON([TagsJson])", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM [Products]", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sql_server_json_execution_failure_marks_json_invalid()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db);
        var report = new ConsolidationVerificationReport();
        var method = typeof(DatabaseConsolidationService).GetMethod(
            "VerifySqlServerJsonTableAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            service,
            [report, "Product", "Products", new[] { "ImagesJson" }, false, CancellationToken.None]));
        await Assert.ThrowsAnyAsync<Exception>(() => task);

        Assert.False(report.IsJsonValid);
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
