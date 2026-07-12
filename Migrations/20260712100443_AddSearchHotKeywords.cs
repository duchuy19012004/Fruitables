using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchHotKeywords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchHotKeywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Text = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchHotKeywords", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SearchHotKeywords",
                columns: new[] { "Id", "CreatedAt", "IsActive", "NormalizedText", "Text", "Weight" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "tao", "táo", 100 },
                    { 2, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "cam", "cam", 90 },
                    { 3, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "nho", "nho", 80 },
                    { 4, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "dau", "dâu", 80 },
                    { 5, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "rau cu", "rau củ", 95 },
                    { 6, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "trai cay", "trái cây", 95 },
                    { 7, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "combo", "combo", 85 },
                    { 8, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "tao fuji", "táo fuji", 70 },
                    { 9, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "chuoi", "chuối", 70 },
                    { 10, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "bo", "bơ", 70 },
                    { 11, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "xoai", "xoài", 70 },
                    { 12, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), true, "nuoc ep", "nước ép", 60 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchHotKeywords_IsActive",
                table: "SearchHotKeywords",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SearchHotKeywords_NormalizedText",
                table: "SearchHotKeywords",
                column: "NormalizedText");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchHotKeywords");
        }
    }
}
