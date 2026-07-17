using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceSchedulingAndVariantPurchasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some existing databases were created from a schema where this legacy
            // index had already been removed even though the migration history says
            // the original migration ran. Keep this migration safe for both shapes.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_CartItems_CartId'
                      AND object_id = OBJECT_ID(N'[dbo].[CartItems]')
                )
                    DROP INDEX [IX_CartItems_CartId] ON [dbo].[CartItems];
                """);

            migrationBuilder.AddColumn<int>(
                name: "ProductVariantId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantName",
                table: "OrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantSKU",
                table: "OrderItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductVariantId",
                table: "CartItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PriceSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductVariantId = table.Column<int>(type: "int", nullable: true),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceSchedules_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceSchedules_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceSchedules_Users_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.Sql("""
                DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

                INSERT INTO PriceSchedules
                    (ProductId, ProductVariantId, DiscountType, Value, StartsAt, EndsAt,
                     IsCancelled, CreatedByAdminId, CreatedAt, UpdatedAt)
                SELECT p.Id, NULL, 0, p.SalePrice, @now, NULL, 0, NULL, @now, @now
                FROM Products p
                WHERE p.SalePrice IS NOT NULL AND p.SalePrice >= 0 AND p.SalePrice < p.Price
                  AND NOT EXISTS (
                      SELECT 1 FROM ProductVariants v WHERE v.ProductId = p.Id AND v.IsActive = 1
                  );

                INSERT INTO PriceSchedules
                    (ProductId, ProductVariantId, DiscountType, Value, StartsAt, EndsAt,
                     IsCancelled, CreatedByAdminId, CreatedAt, UpdatedAt)
                SELECT ProductId, Id, 0, SalePrice, @now, NULL, 0, NULL, @now, @now
                FROM ProductVariants
                WHERE SalePrice IS NOT NULL AND SalePrice >= 0 AND SalePrice < Price;

                -- A product-level legacy sale cannot remain product-level once active
                -- variants exist. Apply it as a fallback to variants that do not have
                -- their own valid legacy sale, while preserving variant-specific sales.
                INSERT INTO PriceSchedules
                    (ProductId, ProductVariantId, DiscountType, Value, StartsAt, EndsAt,
                     IsCancelled, CreatedByAdminId, CreatedAt, UpdatedAt)
                SELECT v.ProductId, v.Id, 0, p.SalePrice, @now, NULL, 0, NULL, @now, @now
                FROM ProductVariants v
                INNER JOIN Products p ON p.Id = v.ProductId
                WHERE v.IsActive = 1
                  AND p.SalePrice IS NOT NULL AND p.SalePrice >= 0 AND p.SalePrice < v.Price
                  AND NOT (v.SalePrice IS NOT NULL AND v.SalePrice >= 0 AND v.SalePrice < v.Price);
                """);

            migrationBuilder.DropColumn(name: "SalePrice", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "SalePrice", table: "Products");

            migrationBuilder.Sql("""
                ;WITH Totals AS (
                    SELECT CartId, ProductId, MIN(Id) AS KeepId, SUM(Quantity) AS TotalQuantity
                    FROM CartItems
                    GROUP BY CartId, ProductId
                )
                UPDATE item SET Quantity = totals.TotalQuantity
                FROM CartItems item
                INNER JOIN Totals totals ON item.Id = totals.KeepId;

                ;WITH Ranked AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY CartId, ProductId ORDER BY Id) AS RowNumber
                    FROM CartItems
                )
                DELETE FROM Ranked WHERE RowNumber > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductVariantId",
                table: "OrderItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId_ProductVariantId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[ProductVariantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId_NoVariant",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true,
                filter: "[ProductVariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductVariantId",
                table: "CartItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceSchedules_CreatedByAdminId",
                table: "PriceSchedules",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceSchedules_ProductId_ProductVariantId_StartsAt",
                table: "PriceSchedules",
                columns: new[] { "ProductId", "ProductVariantId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceSchedules_ProductVariantId",
                table: "PriceSchedules",
                column: "ProductVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_ProductVariants_ProductVariantId",
                table: "CartItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // SQL Server rejects SetNull here: Products cascades to both OrderItems
            // and ProductVariants, so a SetNull through ProductVariants would create
            // multiple cascade paths (error 1785). Restrict matches app behavior:
            // DeleteVariantAsync soft-deletes variants referenced by order items.
            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_ProductVariants_ProductVariantId",
                table: "OrderItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_ProductVariants_ProductVariantId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_ProductVariants_ProductVariantId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductVariantId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId_ProductVariantId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId_NoVariant",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_ProductVariantId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VariantName",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VariantSKU",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "CartItems");

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "ProductVariants",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "Products",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE p SET SalePrice = s.Value
                FROM Products p
                INNER JOIN PriceSchedules s ON s.ProductId = p.Id AND s.ProductVariantId IS NULL
                WHERE s.DiscountType = 0 AND s.IsCancelled = 0
                  AND s.StartsAt <= SYSDATETIMEOFFSET()
                  AND (s.EndsAt IS NULL OR s.EndsAt > SYSDATETIMEOFFSET());

                UPDATE v SET SalePrice = s.Value
                FROM ProductVariants v
                INNER JOIN PriceSchedules s ON s.ProductVariantId = v.Id
                WHERE s.DiscountType = 0 AND s.IsCancelled = 0
                  AND s.StartsAt <= SYSDATETIMEOFFSET()
                  AND (s.EndsAt IS NULL OR s.EndsAt > SYSDATETIMEOFFSET());
                """);

            migrationBuilder.DropTable(name: "PriceSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId",
                table: "CartItems",
                column: "CartId");
        }
    }
}
