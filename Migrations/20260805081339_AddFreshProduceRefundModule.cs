using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddFreshProduceRefundModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReturnRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimDeadlineAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupplementDeadlineAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplementCount = table.Column<int>(type: "int", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ApprovedShippingFeeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CustomerNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequests", x => x.Id);
                    table.CheckConstraint("CK_ReturnRequests_AmountsNonNegative", "[RequestedAmount] >= 0 AND [ApprovedAmount] >= 0 AND [ApprovedShippingFeeAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ShippingFeeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ProcessedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_AmountsNonNegative", "[Amount] >= 0 AND [ShippingFeeAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_Refunds_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Refunds_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    DecisionStatus = table.Column<int>(type: "int", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ApprovedQuantity = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequestItems", x => x.Id);
                    table.CheckConstraint("CK_ReturnRequestItems_AmountsNonNegative", "[RequestedAmount] >= 0 AND [ApprovedAmount] >= 0");
                    table.CheckConstraint("CK_ReturnRequestItems_ApprovedQuantityNonNegative", "[ApprovedQuantity] >= 0");
                    table.CheckConstraint("CK_ReturnRequestItems_ApprovedQuantityWithinRequested", "[ApprovedQuantity] <= [RequestedQuantity]");
                    table.CheckConstraint("CK_ReturnRequestItems_RequestedQuantityPositive", "[RequestedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_ReturnRequestItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequestItems_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    ReturnRequestItemId = table.Column<int>(type: "int", nullable: true),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnEvents_ReturnRequestItems_ReturnRequestItemId",
                        column: x => x.ReturnRequestItemId,
                        principalTable: "ReturnRequestItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReturnEvents_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReturnEvidence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    ReturnRequestItemId = table.Column<int>(type: "int", nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnEvidence_ReturnRequestItems_ReturnRequestItemId",
                        column: x => x.ReturnRequestItemId,
                        principalTable: "ReturnRequestItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReturnEvidence_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnEvidence_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CreatedByUserId",
                table: "Refunds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrderId",
                table: "Refunds",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ProcessedByUserId",
                table: "Refunds",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReturnRequestId",
                table: "Refunds",
                column: "ReturnRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_TransactionReference",
                table: "Refunds",
                column: "TransactionReference",
                unique: true,
                filter: "[TransactionReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvents_ActorUserId",
                table: "ReturnEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvents_ReturnRequestId_CreatedAtUtc",
                table: "ReturnEvents",
                columns: new[] { "ReturnRequestId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvents_ReturnRequestItemId",
                table: "ReturnEvents",
                column: "ReturnRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidence_ReturnRequestId",
                table: "ReturnEvidence",
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidence_ReturnRequestItemId",
                table: "ReturnEvidence",
                column: "ReturnRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidence_StorageKey",
                table: "ReturnEvidence",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidence_UploadedByUserId",
                table: "ReturnEvidence",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItems_OrderItemId",
                table: "ReturnRequestItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItems_ReturnRequestId_OrderItemId",
                table: "ReturnRequestItems",
                columns: new[] { "ReturnRequestId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_OrderId",
                table: "ReturnRequests",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_ReturnNumber",
                table: "ReturnRequests",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_Status_ClaimDeadlineAtUtc",
                table: "ReturnRequests",
                columns: new[] { "Status", "ClaimDeadlineAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_UserId_SubmittedAtUtc",
                table: "ReturnRequests",
                columns: new[] { "UserId", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ReturnEvents");

            migrationBuilder.DropTable(
                name: "ReturnEvidence");

            migrationBuilder.DropTable(
                name: "ReturnRequestItems");

            migrationBuilder.DropTable(
                name: "ReturnRequests");
        }
    }
}
