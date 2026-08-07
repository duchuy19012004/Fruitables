using Fruitables.Data;
using Fruitables.Migrations;
using Fruitables.Services.Infrastructure.DatabaseConsolidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;
using Xunit;

namespace Fruitables.Tests;

public sealed class MigrationGuardTests
{
    [Fact]
    public void AddAggregateJsonSchema_is_additive_and_does_not_drop_legacy_tables()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migrationFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot, "Migrations"),
            "*_AddAggregateJsonSchema.cs");

        var migrationFile = Assert.Single(migrationFiles);
        var source = File.ReadAllText(migrationFile);
        var upMethod = source[..source.IndexOf("protected override void Down", StringComparison.Ordinal)];

        Assert.DoesNotContain("DropTable(", upMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn(", upMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("RenameTable(", upMethod, StringComparison.Ordinal);
        Assert.Contains("CreateTable(", upMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractAggregateSchema_drops_only_approved_legacy_tables()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migrationFile = Assert.Single(Directory.GetFiles(
            Path.Combine(repositoryRoot, "Migrations"),
            "*_ContractAggregateSchema.cs"));
        var source = File.ReadAllText(migrationFile);
        var upMethod = source[..source.IndexOf("protected override void Down", StringComparison.Ordinal)];
        var drops = System.Text.RegularExpressions.Regex.Matches(
                upMethod,
                "DropTable\\(\\s*name: \\\"([^\\\"]+)\\\"",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CartItems", "ChatMessages", "ComboAuditLogs", "ComboItems", "ContactMessages", "Coupons",
            "Faqs", "OrderNotes", "OrderStatusHistories", "PriceSchedules", "ProductImages", "ProductLogs",
            "ProductTagMapping", "RbacAuditLogs", "Refunds", "ReturnEvents", "ReturnEvidence", "ReviewHelpfuls",
            "ReviewReports", "ReviewSentimentAspects", "RolePermissions", "SearchHotKeywords", "SePayTransactions",
            "Testimonials", "UserAccountLogs", "UserRoleMappings", "Wishlists", "CartGroups", "ProductTags",
            "ReturnRequestItems", "ReviewSentiments", "Permissions", "Combos", "ReturnRequests"
        };
        Assert.Equal(approved, drops);
        foreach (var retained in new[]
        {
            "OrderItems", "Products", "ProductVariants", "Orders", "Users", "Roles", "Addresses",
            "Categories", "Settings", "KnowledgeChunks", "OutboxMessages", "Payments", "Promotions",
            "Reviews", "Returns", "ContentEntries", "ChatSessions", "AuditLogs", "Carts"
        })
            Assert.DoesNotContain($"name: \"{retained}\"", upMethod, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Consolidation_identity_migration_preserves_colliding_audit_rows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        var longEntityType = new string('E', 100);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE [AuditLogs]
            (
                [Id] INTEGER NOT NULL PRIMARY KEY,
                [Action] TEXT NOT NULL,
                [EntityType] TEXT NOT NULL,
                [EntityId] INTEGER NOT NULL,
                [ChangedByAdminId] INTEGER NOT NULL,
                [ChangedAt] TEXT NOT NULL,
                [OldValue] TEXT NULL,
                [NewValue] TEXT NULL,
                [SourceId] INTEGER NULL,
                [SourceType] TEXT NULL
            );
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [AuditLogs] ([Id], [Action], [EntityType], [EntityId], [ChangedByAdminId], [ChangedAt])
            VALUES (11, 'Update', {longEntityType}, 10, 1, '2026-08-07T00:00:00Z');
            INSERT INTO [AuditLogs] ([Id], [Action], [EntityType], [EntityId], [ChangedByAdminId], [ChangedAt])
            VALUES (12, 'Update', {longEntityType}, 10, 1, '2026-08-07T00:01:00Z');
            """);

        await db.Database.ExecuteSqlRawAsync(DatabaseConsolidationSql.BuildHistoricalAuditIdentityUpdate());
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX [IX_AuditLogs_SourceType_SourceId] ON [AuditLogs] ([SourceType], [SourceId]);");

        var rows = await db.Database
            .SqlQueryRaw<AuditIdentity>("SELECT [SourceType] AS [SourceType], [SourceId] AS [SourceId] FROM [AuditLogs] ORDER BY [Id]")
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(new AuditIdentity("LegacyAudit", -11), rows[0]);
        Assert.Equal(new AuditIdentity("LegacyAudit", -12), rows[1]);
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [AuditLogs]").SingleAsync());
    }

    [Fact]
    public void Final_aggregate_columns_exist_before_contract_migration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var additive = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Migrations",
            "20260806224359_AddConsolidationIdentityAndPaymentStatus.cs"));
        var contract = File.ReadAllText(Directory.GetFiles(
            Path.Combine(repositoryRoot, "Migrations"),
            "*_ContractAggregateSchema.cs").Single());

        var additiveUp = additive[..additive.IndexOf("protected override void Down", StringComparison.Ordinal)];
        var contractUp = contract[..contract.IndexOf("protected override void Down", StringComparison.Ordinal)];
        Assert.Contains("name: \"AssetRevision\"", additiveUp, StringComparison.Ordinal);
        Assert.Contains("name: \"CustomerCode\"", additiveUp, StringComparison.Ordinal);
        Assert.DoesNotContain("name: \"AssetRevision\"", contractUp, StringComparison.Ordinal);
        Assert.DoesNotContain("name: \"CustomerCode\"", contractUp, StringComparison.Ordinal);
    }

    [Fact]
    public void Consolidation_identity_migration_up_contains_reserved_historical_namespace()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var migration = new AddConsolidationIdentityAndPaymentStatus();
        var up = typeof(AddConsolidationIdentityAndPaymentStatus).GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);

        up!.Invoke(migration, [migrationBuilder]);

        var auditSql = migrationBuilder.Operations
            .OfType<SqlOperation>()
            .Single(operation => operation.Sql.Contains("[AuditLogs]", StringComparison.Ordinal));
        Assert.Contains("LegacyAudit", auditSql.Sql, StringComparison.Ordinal);
        Assert.Contains("-CAST([AuditLogs].[Id] AS bigint)", auditSql.Sql, StringComparison.Ordinal);
        Assert.Contains(
            migrationBuilder.Operations.OfType<CreateIndexOperation>(),
            operation => operation.Name == "IX_AuditLogs_SourceType_SourceId" && operation.IsUnique);
    }

    private sealed record AuditIdentity(string SourceType, long SourceId);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fruitables.csproj")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
