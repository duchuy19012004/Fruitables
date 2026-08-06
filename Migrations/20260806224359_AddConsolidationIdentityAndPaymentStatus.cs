using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddConsolidationIdentityAndPaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProviderEventStatus",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SourceId",
                table: "AuditLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [AuditLogs]
                SET [SourceType] = [EntityType],
                    [SourceId] = CASE
                        WHEN EXISTS (
                            SELECT 1
                            FROM [AuditLogs] AS [prior]
                            WHERE [prior].[EntityType] = [AuditLogs].[EntityType]
                              AND [prior].[EntityId] = [AuditLogs].[EntityId]
                              AND [prior].[Id] < [AuditLogs].[Id])
                        THEN -CAST([AuditLogs].[Id] AS bigint)
                        ELSE CAST([AuditLogs].[EntityId] AS bigint)
                    END
                WHERE [SourceType] IS NULL OR [SourceId] IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "SourceId",
                table: "AuditLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceType",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.Sql(
                "UPDATE [Payments] SET [ProviderEventStatus] = COALESCE((SELECT [Status] FROM [SePayTransactions] WHERE [SePayTransactions].[SePayTransactionId] = TRY_CONVERT(bigint, [Payments].[ProviderTransactionId])), 0) WHERE [Payments].[Provider] = 'SePay';");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderEventStatus",
                table: "Payments",
                column: "ProviderEventStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SourceType_SourceId",
                table: "AuditLogs",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ProviderEventStatus",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_SourceType_SourceId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ProviderEventStatus",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "AuditLogs");
        }
    }
}
