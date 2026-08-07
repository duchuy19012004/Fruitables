using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class HardenPriceProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutRequestId",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutSessionId",
                table: "Orders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM Products
                    WHERE Price <= 0 OR Price <> FLOOR(Price) OR Price > 99999999
                )
                    THROW 51001, 'Invalid product base prices must be repaired before HardenPriceProduction.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM ProductVariants
                    WHERE Price <= 0 OR Price <> FLOOR(Price) OR Price > 99999999
                )
                    THROW 51002, 'Invalid product variant base prices must be repaired before HardenPriceProduction.', 1;
                """);

            migrationBuilder.Sql("""
                DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

                UPDATE schedule
                SET IsCancelled = 1,
                    CancelledAt = @now,
                    UpdatedAt = @now,
                    CancellationReason = LEFT('Tự động hủy: dữ liệu lịch giá không hợp lệ khi nâng cấp production.', 500)
                FROM PriceSchedules schedule
                INNER JOIN Products product ON product.Id = schedule.ProductId
                LEFT JOIN ProductVariants variant ON variant.Id = schedule.ProductVariantId
                CROSS APPLY (
                    SELECT CASE
                        WHEN schedule.ProductVariantId IS NULL THEN product.Price
                        ELSE variant.Price
                    END AS BasePrice
                ) base_price
                WHERE schedule.IsCancelled = 0
                  AND (
                      schedule.DiscountType NOT IN (0, 1)
                      OR (
                          schedule.DiscountType = 0
                          AND (
                              schedule.Value <= 0
                              OR schedule.Value <> FLOOR(schedule.Value)
                              OR schedule.Value >= base_price.BasePrice
                          )
                      )
                       OR (
                           schedule.DiscountType = 1
                          AND (
                              schedule.Value < 1
                              OR schedule.Value > 99
                               OR ROUND(base_price.BasePrice * (100 - schedule.Value) / 100, 0) <= 0
                           )
                       )
                       OR (schedule.EndsAt IS NOT NULL AND schedule.EndsAt <= schedule.StartsAt)
                   );
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT SessionId
                    FROM Carts
                    WHERE SessionId IS NOT NULL
                    GROUP BY SessionId
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Duplicate non-null cart sessions must be merged before HardenPriceProduction.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CheckoutSessionId_CheckoutRequestId",
                table: "Orders",
                columns: new[] { "CheckoutSessionId", "CheckoutRequestId" },
                unique: true,
                filter: "[CheckoutSessionId] IS NOT NULL AND [CheckoutRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_SessionId",
                table: "Carts",
                column: "SessionId",
                unique: true,
                filter: "[SessionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CheckoutSessionId_CheckoutRequestId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Carts_SessionId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "CheckoutRequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CheckoutSessionId",
                table: "Orders");
        }
    }
}
