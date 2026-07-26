using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class OperationalizeCommercialCombos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComboQuantity",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndsAt",
                table: "Combos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartsAt",
                table: "Combos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Combos",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql("UPDATE [Combos] SET [Status] = 3 WHERE [IsActive] = 0;");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "CartGroups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("UPDATE [CartGroups] SET [ExpiresAt] = DATEADD(day, 30, GETUTCDATE());");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAt",
                table: "CartGroups",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ComboAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComboId = table.Column<int>(type: "int", nullable: true),
                    AdminId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComboAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComboAuditLogs_Combos_ComboId",
                        column: x => x.ComboId,
                        principalTable: "Combos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ComboAuditLogs_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Combos_Status_StartsAt_EndsAt",
                table: "Combos",
                columns: new[] { "Status", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CartGroups_ExpiresAt",
                table: "CartGroups",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CartGroups_UpdatedAt",
                table: "CartGroups",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComboAuditLogs_AdminId",
                table: "ComboAuditLogs",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ComboAuditLogs_ComboId_CreatedAt",
                table: "ComboAuditLogs",
                columns: new[] { "ComboId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComboAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Combos_Status_StartsAt_EndsAt",
                table: "Combos");

            migrationBuilder.DropIndex(
                name: "IX_CartGroups_ExpiresAt",
                table: "CartGroups");

            migrationBuilder.DropIndex(
                name: "IX_CartGroups_UpdatedAt",
                table: "CartGroups");

            migrationBuilder.DropColumn(
                name: "ComboQuantity",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "EndsAt",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "StartsAt",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Combos");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "CartGroups");
        }
    }
}
