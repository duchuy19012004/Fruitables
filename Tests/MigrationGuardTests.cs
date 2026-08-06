using Fruitables.Data;
using Fruitables.Services.Infrastructure.DatabaseConsolidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    public async Task Consolidation_identity_migration_preserves_colliding_audit_rows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
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
            INSERT INTO [AuditLogs] ([Id], [Action], [EntityType], [EntityId], [ChangedByAdminId], [ChangedAt])
            VALUES (11, 'Update', 'Product', 10, 1, '2026-08-07T00:00:00Z');
            INSERT INTO [AuditLogs] ([Id], [Action], [EntityType], [EntityId], [ChangedByAdminId], [ChangedAt])
            VALUES (12, 'Update', 'Product', 10, 1, '2026-08-07T00:01:00Z');
            """);

        await db.Database.ExecuteSqlRawAsync(DatabaseConsolidationSql.BuildHistoricalAuditIdentityUpdate());
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX [IX_AuditLogs_SourceType_SourceId] ON [AuditLogs] ([SourceType], [SourceId]);");

        var migrationPath = Path.Combine(FindRepositoryRoot(), "Migrations", "20260806224359_AddConsolidationIdentityAndPaymentStatus.cs");
        var migrationSource = File.ReadAllText(migrationPath);
        Assert.Contains("THEN -CAST([AuditLogs].[Id] AS bigint)", migrationSource, StringComparison.Ordinal);

        var rows = await db.Database
            .SqlQueryRaw<AuditIdentity>("SELECT [SourceType] AS [SourceType], [SourceId] AS [SourceId] FROM [AuditLogs] ORDER BY [Id]")
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(new AuditIdentity("Product", 10), rows[0]);
        Assert.Equal(new AuditIdentity("Product", -12), rows[1]);
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [AuditLogs]").SingleAsync());
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
