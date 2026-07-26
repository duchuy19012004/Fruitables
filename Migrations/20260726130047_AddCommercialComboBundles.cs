using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialComboBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId_NoVariant",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId_ProductVariantId",
                table: "CartItems");

            migrationBuilder.AddColumn<decimal>(
                name: "ComboDiscount",
                table: "OrderItems",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ComboNameSnapshot",
                table: "OrderItems",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComboRevision",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceComboId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowCouponStacking",
                table: "Combos",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Combos",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedPrice",
                table: "Combos",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingType",
                table: "Combos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Combos",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CartGroupId",
                table: "CartItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComboDiscount",
                table: "CartItems",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CartGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartId = table.Column<int>(type: "int", nullable: false),
                    ComboId = table.Column<int>(type: "int", nullable: false),
                    ComboRevision = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ComboName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OriginalTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    FinalTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AllowCouponStacking = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartGroups_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartGroups_Combos_ComboId",
                        column: x => x.ComboId,
                        principalTable: "Combos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartGroupId_ProductId_NoVariant",
                table: "CartItems",
                columns: new[] { "CartGroupId", "ProductId" },
                unique: true,
                filter: "[CartGroupId] IS NOT NULL AND [ProductVariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartGroupId_ProductId_ProductVariantId",
                table: "CartItems",
                columns: new[] { "CartGroupId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[CartGroupId] IS NOT NULL AND [ProductVariantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId_NoVariant",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true,
                filter: "[CartGroupId] IS NULL AND [ProductVariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId_ProductVariantId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[CartGroupId] IS NULL AND [ProductVariantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CartGroups_CartId_ComboId_ComboRevision",
                table: "CartGroups",
                columns: new[] { "CartId", "ComboId", "ComboRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartGroups_ComboId",
                table: "CartGroups",
                column: "ComboId");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_CartGroups_CartGroupId",
                table: "CartItems",
                column: "CartGroupId",
                principalTable: "CartGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_CartGroups_CartGroupId",
                table: "CartItems");

            migrationBuilder.DropTable(
                name: "CartGroups");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartGroupId_ProductId_NoVariant",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartGroupId_ProductId_ProductVariantId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId_NoVariant",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId_ProductVariantId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ComboDiscount",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ComboNameSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ComboRevision",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SourceComboId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "AllowCouponStacking",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "FixedPrice",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "PricingType",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "CartGroupId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ComboDiscount",
                table: "CartItems");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId_NoVariant",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true,
                filter: "[ProductVariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId_ProductVariantId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[ProductVariantId] IS NOT NULL");
        }
    }
}
