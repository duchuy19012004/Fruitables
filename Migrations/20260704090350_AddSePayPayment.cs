using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddSePayPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentCode",
                table: "Orders",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SePayTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SePayTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    PaymentCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TransferAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ReferenceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SePayTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SePayTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentCode",
                table: "Orders",
                column: "PaymentCode",
                unique: true,
                filter: "[PaymentCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SePayTransactions_OrderId",
                table: "SePayTransactions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SePayTransactions_PaymentCode",
                table: "SePayTransactions",
                column: "PaymentCode");

            migrationBuilder.CreateIndex(
                name: "IX_SePayTransactions_SePayTransactionId",
                table: "SePayTransactions",
                column: "SePayTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SePayTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentCode",
                table: "Orders");
        }
    }
}
