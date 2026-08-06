using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Fruitables.Tests;

public sealed class AggregateJsonModelConfigurationTests
{
    [Fact]
    public void Target_aggregate_tables_are_present_in_the_ef_model()
    {
        using var context = CreateSqlServerContext();

        var tables = new[]
        {
            (typeof(Payment), "Payments"),
            (typeof(Promotion), "Promotions"),
            (typeof(ContentEntry), "ContentEntries"),
            (typeof(AuditLog), "AuditLogs"),
            (typeof(ReturnCase), "Returns")
        };

        foreach (var (entityType, tableName) in tables)
        {
            var entity = context.Model.FindEntityType(entityType);

            Assert.NotNull(entity);
            Assert.Equal(tableName, entity!.GetTableName());
        }
    }

    [Theory]
    [InlineData(typeof(Product), nameof(Product.ImagesJson), "[]")]
    [InlineData(typeof(Product), nameof(Product.TagsJson), "[]")]
    [InlineData(typeof(User), nameof(User.RoleIdsJson), "[]")]
    [InlineData(typeof(User), nameof(User.WishlistJson), "[]")]
    [InlineData(typeof(Role), nameof(Role.PermissionsJson), "[]")]
    [InlineData(typeof(Cart), nameof(Cart.LinesJson), "[]")]
    [InlineData(typeof(Order), nameof(Order.StatusHistoryJson), "[]")]
    [InlineData(typeof(Order), nameof(Order.NotesJson), "[]")]
    [InlineData(typeof(Review), nameof(Review.MetadataJson), "{ \"schemaVersion\": 1 }")]
    [InlineData(typeof(ChatSession), nameof(ChatSession.MessagesJson), "[]")]
    [InlineData(typeof(Promotion), nameof(Promotion.PayloadJson), "{ \"schemaVersion\": 1 }")]
    [InlineData(typeof(ContentEntry), nameof(ContentEntry.PayloadJson), "{ \"schemaVersion\": 1 }")]
    [InlineData(typeof(ReturnCase), nameof(ReturnCase.DetailsJson), "{ \"schemaVersion\": 1 }")]
    public void Json_documents_are_required_nvarchar_max_columns_with_literal_defaults(
        Type entityClrType,
        string propertyName,
        string defaultValue)
    {
        using var context = CreateSqlServerContext();

        var property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
        Assert.Equal("nvarchar(max)", property.GetColumnType());
        Assert.Equal(defaultValue, property.GetDefaultValue());
    }

    [Fact]
    public void Json_documents_have_sql_server_isjson_checks()
    {
        using var context = CreateSqlServerContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var jsonProperties = new[]
        {
            (typeof(Product), nameof(Product.ImagesJson)),
            (typeof(Product), nameof(Product.TagsJson)),
            (typeof(User), nameof(User.RoleIdsJson)),
            (typeof(User), nameof(User.WishlistJson)),
            (typeof(Role), nameof(Role.PermissionsJson)),
            (typeof(Cart), nameof(Cart.LinesJson)),
            (typeof(Order), nameof(Order.StatusHistoryJson)),
            (typeof(Order), nameof(Order.NotesJson)),
            (typeof(Review), nameof(Review.MetadataJson)),
            (typeof(ChatSession), nameof(ChatSession.MessagesJson)),
            (typeof(Promotion), nameof(Promotion.PayloadJson)),
            (typeof(ContentEntry), nameof(ContentEntry.PayloadJson)),
            (typeof(ReturnCase), nameof(ReturnCase.DetailsJson))
        };

        foreach (var (entityClrType, propertyName) in jsonProperties)
        {
            var entity = model.FindEntityType(entityClrType)!;
            var check = Assert.Single(
                entity.GetCheckConstraints(),
                constraint => constraint.Sql.Contains($"[{propertyName}]", StringComparison.Ordinal)
                    && constraint.Sql.Contains("ISJSON", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(propertyName, check.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Target_identity_and_filter_indexes_are_configured()
    {
        using var context = CreateSqlServerContext();

        var payment = context.Model.FindEntityType(typeof(Payment))!;
        var paymentIdentity = FindIndex(payment, nameof(Payment.Provider), nameof(Payment.ProviderTransactionId));
        Assert.True(paymentIdentity.IsUnique);

        var promotion = context.Model.FindEntityType(typeof(Promotion))!;
        var promotionCode = FindIndex(promotion, nameof(Promotion.Code));
        Assert.True(promotionCode.IsUnique);
        Assert.Equal("[Code] IS NOT NULL", promotionCode.GetFilter());
        Assert.Contains(
            promotion.GetIndexes(),
            index => index.Properties.Count == 1
                && index.Properties[0].Name == nameof(Promotion.Type));

        var content = context.Model.FindEntityType(typeof(ContentEntry))!;
        var contentIdentity = FindIndex(content, nameof(ContentEntry.EntryType), nameof(ContentEntry.Key));
        Assert.True(contentIdentity.IsUnique);

        var returnCase = context.Model.FindEntityType(typeof(ReturnCase))!;
        Assert.True(FindIndex(returnCase, nameof(ReturnCase.OrderId)).IsUnique);
    }

    [Fact]
    public void Mutable_aggregate_roots_have_row_version_concurrency_tokens()
    {
        using var context = CreateSqlServerContext();

        var aggregateTypes = new[]
        {
            typeof(User),
            typeof(Role),
            typeof(Product),
            typeof(Cart),
            typeof(Order),
            typeof(Review),
            typeof(ChatSession),
            typeof(Payment),
            typeof(Promotion),
            typeof(ContentEntry),
            typeof(ReturnCase)
        };

        foreach (var aggregateType in aggregateTypes)
        {
            var rowVersion = context.Model.FindEntityType(aggregateType)!.FindProperty("RowVersion");

            Assert.NotNull(rowVersion);
            Assert.True(rowVersion!.IsConcurrencyToken);
        }
    }

    private static IIndex FindIndex(IEntityType entity, params string[] propertyNames)
    {
        return Assert.Single(
            entity.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }

    private static ApplicationDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=FruitablesModelConfigurationTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }
}
