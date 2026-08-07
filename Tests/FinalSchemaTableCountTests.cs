using Fruitables.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fruitables.Tests;

public sealed class FinalSchemaTableCountTests
{
    private static readonly string[] TargetBusinessTables =
    [
        "Users", "Roles", "Addresses", "Categories", "Products", "ProductVariants",
        "Carts", "Orders", "OrderItems", "Payments", "Promotions", "Reviews", "Returns",
        "Settings", "ContentEntries", "ChatSessions", "KnowledgeChunks", "AuditLogs",
        "OutboxMessages"
    ];

    [Fact]
    public void Ef_model_contains_all_19_target_business_tables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=FruitablesContractModel;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new ApplicationDbContext(options);
        var tables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(19, TargetBusinessTables.Length);
        Assert.Equal(TargetBusinessTables.Length, tables.Count);
        Assert.Equal(
            TargetBusinessTables.Order(StringComparer.OrdinalIgnoreCase),
            tables.Order(StringComparer.OrdinalIgnoreCase));
    }
}
