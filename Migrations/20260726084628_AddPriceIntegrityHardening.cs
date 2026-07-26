using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceIntegrityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PriceRevision",
                table: "ProductVariants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PriceRevision",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "PriceSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "PriceSchedules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByAdminId",
                table: "PriceSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "PriceSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PriceSchedules_CancelledByAdminId",
                table: "PriceSchedules",
                column: "CancelledByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceSchedules_Users_CancelledByAdminId",
                table: "PriceSchedules",
                column: "CancelledByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceSchedules_Users_CancelledByAdminId",
                table: "PriceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_PriceSchedules_CancelledByAdminId",
                table: "PriceSchedules");

            migrationBuilder.DropColumn(
                name: "PriceRevision",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "PriceRevision",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "PriceSchedules");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "PriceSchedules");

            migrationBuilder.DropColumn(
                name: "CancelledByAdminId",
                table: "PriceSchedules");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "PriceSchedules");
        }
    }
}
