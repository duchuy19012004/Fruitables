using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class HardenComboPhaseOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ;WITH Ranked AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY ComboId, ProductId, ProductVariantId
                               ORDER BY Id) AS RowNumber,
                           SUM(Quantity) OVER (
                               PARTITION BY ComboId, ProductId, ProductVariantId) AS TotalQuantity
                    FROM ComboItems
                )
                UPDATE item
                SET Quantity = ranked.TotalQuantity
                FROM ComboItems item
                INNER JOIN Ranked ranked ON ranked.Id = item.Id
                WHERE ranked.RowNumber = 1;

                ;WITH Ranked AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY ComboId, ProductId, ProductVariantId
                               ORDER BY Id) AS RowNumber
                    FROM ComboItems
                )
                DELETE item
                FROM ComboItems item
                INNER JOIN Ranked ranked ON ranked.Id = item.Id
                WHERE ranked.RowNumber > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ComboItems_ComboId_ProductId_NoVariant",
                table: "ComboItems",
                columns: new[] { "ComboId", "ProductId" },
                unique: true,
                filter: "[ProductVariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ComboItems_ComboId_ProductId_ProductVariantId",
                table: "ComboItems",
                columns: new[] { "ComboId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[ProductVariantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ComboItems_ComboId_ProductId_NoVariant",
                table: "ComboItems");

            migrationBuilder.DropIndex(
                name: "IX_ComboItems_ComboId_ProductId_ProductVariantId",
                table: "ComboItems");
        }
    }
}
