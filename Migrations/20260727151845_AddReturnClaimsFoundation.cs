using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnClaimsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReturnPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    ClaimWindowHours = table.Column<int>(type: "int", nullable: false),
                    EvidenceRequired = table.Column<bool>(type: "bit", nullable: false),
                    AllowPartialRefund = table.Column<bool>(type: "bit", nullable: false),
                    AllowFullRefund = table.Column<bool>(type: "bit", nullable: false),
                    AllowReplacement = table.Column<bool>(type: "bit", nullable: false),
                    AllowStoreCredit = table.Column<bool>(type: "bit", nullable: false),
                    AllowRestock = table.Column<bool>(type: "bit", nullable: false),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnPolicies_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnPolicies_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Resolution = table.Column<int>(type: "int", nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimDeadlineAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerId = table.Column<int>(type: "int", nullable: true),
                    CustomerNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MerchantFault = table.Column<bool>(type: "bit", nullable: false),
                    ShippingFeeApproved = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
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
                name: "ReturnEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnEvents_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    ReturnPolicyId = table.Column<int>(type: "int", nullable: true),
                    RequestedQuantity = table.Column<int>(type: "int", nullable: false),
                    ApprovedQuantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    RequestedResolution = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NetPaidAmountSnapshot = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PolicyVersionSnapshot = table.Column<int>(type: "int", nullable: false),
                    ClaimWindowHoursSnapshot = table.Column<int>(type: "int", nullable: false),
                    EvidenceRequiredSnapshot = table.Column<bool>(type: "bit", nullable: false),
                    ClaimDeadlineAtUtcSnapshot = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequestItems", x => x.Id);
                    table.CheckConstraint("CK_ReturnRequestItems_ApprovedQuantity", "[ApprovedQuantity] >= 0 AND [ApprovedQuantity] <= [RequestedQuantity]");
                    table.CheckConstraint("CK_ReturnRequestItems_RequestedQuantity", "[RequestedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_ReturnRequestItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequestItems_ReturnPolicies_ReturnPolicyId",
                        column: x => x.ReturnPolicyId,
                        principalTable: "ReturnPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequestItems_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDispositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    InspectorUserId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDispositions", x => x.Id);
                    table.CheckConstraint("CK_InventoryDispositions_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryDispositions_ReturnRequestItems_ReturnRequestItemId",
                        column: x => x.ReturnRequestItemId,
                        principalTable: "ReturnRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDispositions_Users_InspectorUserId",
                        column: x => x.InspectorUserId,
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
                    ReturnRequestItemId = table.Column<int>(type: "int", nullable: true),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TransferEvidenceStorageKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ProcessedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Refunds_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_ReturnRequestItems_ReturnRequestItemId",
                        column: x => x.ReturnRequestItemId,
                        principalTable: "ReturnRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    ReturnRequestItemId = table.Column<int>(type: "int", nullable: true),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScanStatus = table.Column<int>(type: "int", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnEvidences_ReturnRequestItems_ReturnRequestItemId",
                        column: x => x.ReturnRequestItemId,
                        principalTable: "ReturnRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnEvidences_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnEvidences_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDispositions_InspectorUserId",
                table: "InventoryDispositions",
                column: "InspectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDispositions_ReturnRequestItemId_CreatedAtUtc",
                table: "InventoryDispositions",
                columns: new[] { "ReturnRequestItemId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CreatedByUserId",
                table: "Refunds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_IdempotencyKey",
                table: "Refunds",
                column: "IdempotencyKey",
                unique: true);

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
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReturnRequestItemId",
                table: "Refunds",
                column: "ReturnRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Status_CreatedAtUtc",
                table: "Refunds",
                columns: new[] { "Status", "CreatedAtUtc" });

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
                name: "IX_ReturnEvidences_ReturnRequestId",
                table: "ReturnEvidences",
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidences_ReturnRequestItemId",
                table: "ReturnEvidences",
                column: "ReturnRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidences_StorageKey",
                table: "ReturnEvidences",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidences_UploadedByUserId",
                table: "ReturnEvidences",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnPolicies_CategoryId",
                table: "ReturnPolicies",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnPolicies_ProductId",
                table: "ReturnPolicies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnPolicies_Scope_ProductId_CategoryId_Reason_IsActive",
                table: "ReturnPolicies",
                columns: new[] { "Scope", "ProductId", "CategoryId", "Reason", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnPolicies_Version_EffectiveFromUtc",
                table: "ReturnPolicies",
                columns: new[] { "Version", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItems_OrderItemId",
                table: "ReturnRequestItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItems_ReturnPolicyId",
                table: "ReturnRequestItems",
                column: "ReturnPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItems_ReturnRequestId_OrderItemId",
                table: "ReturnRequestItems",
                columns: new[] { "ReturnRequestId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_OrderId_Status",
                table: "ReturnRequests",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_ReturnNumber",
                table: "ReturnRequests",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_ReviewerId",
                table: "ReturnRequests",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_Status_ReviewDueAtUtc",
                table: "ReturnRequests",
                columns: new[] { "Status", "ReviewDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_UserId_IdempotencyKey",
                table: "ReturnRequests",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_UserId_SubmittedAtUtc",
                table: "ReturnRequests",
                columns: new[] { "UserId", "SubmittedAtUtc" });

            migrationBuilder.Sql(@"
DECLARE @now datetime2 = '2026-07-27T00:00:00Z';
DECLARE @reasons TABLE (Reason int, EvidenceRequired bit, IsEligible bit);
INSERT INTO @reasons VALUES
(0,1,1),(1,1,1),(2,1,1),(3,1,1),(4,1,1),(5,1,1),(6,1,1),(7,1,1),(8,1,1),(9,0,0),(10,0,1);

INSERT INTO ReturnPolicies
(Name, Scope, CategoryId, ProductId, Reason, ClaimWindowHours, EvidenceRequired,
 AllowPartialRefund, AllowFullRefund, AllowReplacement, AllowStoreCredit,
 AllowRestock, IsEligible, IsActive, Version, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc)
SELECT CONCAT('Default v1 - ', Reason), 0, NULL, NULL, Reason, 24, EvidenceRequired,
       IsEligible, IsEligible, IsEligible, IsEligible, 0, IsEligible, 1, 1, @now, NULL, @now
FROM @reasons;

INSERT INTO ReturnPolicies
(Name, Scope, CategoryId, ProductId, Reason, ClaimWindowHours, EvidenceRequired,
 AllowPartialRefund, AllowFullRefund, AllowReplacement, AllowStoreCredit,
 AllowRestock, IsEligible, IsActive, Version, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc)
SELECT CONCAT('Perishable 12h v1 - ', c.Id, ' - ', r.Reason), 1, c.Id, NULL, r.Reason, 12, r.EvidenceRequired,
       r.IsEligible, r.IsEligible, r.IsEligible, r.IsEligible, 0, r.IsEligible, 1, 1, @now, NULL, @now
FROM Categories c CROSS JOIN @reasons r
WHERE LOWER(c.Slug) IN ('rau-la','rau-thom','qua-mong','nam','nam-tuoi','dau-tay')
   OR LOWER(c.Slug) LIKE 'rau-la-%' OR LOWER(c.Slug) LIKE 'rau-thom-%'
   OR LOWER(c.Slug) LIKE '%qua-mong%' OR LOWER(c.Slug) LIKE '%berry%' OR LOWER(c.Slug) LIKE 'nam-%';

INSERT INTO ReturnPolicies
(Name, Scope, CategoryId, ProductId, Reason, ClaimWindowHours, EvidenceRequired,
 AllowPartialRefund, AllowFullRefund, AllowReplacement, AllowStoreCredit,
 AllowRestock, IsEligible, IsActive, Version, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc)
SELECT CONCAT('Sealed dry 7d v1 - ', c.Id, ' - ', r.Reason), 1, c.Id, NULL, r.Reason, 168, r.EvidenceRequired,
       r.IsEligible, r.IsEligible, r.IsEligible, r.IsEligible, 1, r.IsEligible, 1, 1, @now, NULL, @now
FROM Categories c CROSS JOIN @reasons r
WHERE LOWER(c.Slug) IN ('hang-kho','thuc-pham-kho','do-kho','hang-dong-goi')
   OR LOWER(c.Slug) LIKE '%-kho' OR LOWER(c.Slug) LIKE '%dong-goi%' OR LOWER(c.Slug) LIKE '%nguyen-seal%';

DECLARE @permission TABLE (Name nvarchar(100), Description nvarchar(500));
INSERT INTO @permission VALUES
('returns.view',N'Xem hàng đợi khiếu nại'),('returns.review',N'Yêu cầu bằng chứng và xem xét'),
('returns.approve',N'Duyệt khiếu nại'),('returns.reject',N'Từ chối khiếu nại'),
('returns.refund',N'Ghi nhận hoàn tiền'),('returns.override_policy',N'Override policy và hoàn kho');
INSERT INTO Permissions (Name, Description, Module, CreatedAt)
SELECT p.Name,p.Description,'returns',@now FROM @permission p
WHERE NOT EXISTS (SELECT 1 FROM Permissions x WHERE x.Name=p.Name);

INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='SuperAdmin' AND p.Module='returns'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='Admin' AND p.Name IN ('returns.view','returns.review','returns.approve','returns.reject','returns.refund')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='CustomerSupport' AND p.Name IN ('returns.view','returns.review','returns.reject')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE rp FROM RolePermissions rp INNER JOIN Permissions p ON p.Id=rp.PermissionId WHERE p.Module='returns';
DELETE FROM Permissions WHERE Module='returns';");

            migrationBuilder.DropTable(
                name: "InventoryDispositions");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ReturnEvents");

            migrationBuilder.DropTable(
                name: "ReturnEvidences");

            migrationBuilder.DropTable(
                name: "ReturnRequestItems");

            migrationBuilder.DropTable(
                name: "ReturnPolicies");

            migrationBuilder.DropTable(
                name: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "Orders");
        }
    }
}
