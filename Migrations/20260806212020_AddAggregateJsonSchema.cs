using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddAggregateJsonSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoleIdsJson",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "varbinary(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WishlistJson",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "PermissionsJson",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Roles",
                type: "varbinary(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{ \"schemaVersion\": 1 }");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Reviews",
                type: "varbinary(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagesJson",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "varbinary(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "NotesJson",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "StatusHistoryJson",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "MessagesJson",
                table: "ChatSessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChatSessions",
                type: "varbinary(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinesJson",
                table: "Carts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Carts",
                type: "varbinary(16)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    ChangedByAdminId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_NewValue_IsJson", "[NewValue] IS NULL OR ISJSON([NewValue]) = 1");
                    table.CheckConstraint("CK_AuditLogs_OldValue_IsJson", "[OldValue] IS NULL OR ISJSON([OldValue]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "ContentEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{ \"schemaVersion\": 1 }"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentEntries", x => x.Id);
                    table.CheckConstraint("CK_ContentEntries_PayloadJson_IsJson", "ISJSON([PayloadJson]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ReferenceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{ \"schemaVersion\": 1 }"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                    table.CheckConstraint("CK_Promotions_PayloadJson_IsJson", "ISJSON([PayloadJson]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "Returns",
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
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{ \"schemaVersion\": 1 }"),
                    RowVersion = table.Column<byte[]>(type: "varbinary(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Returns", x => x.Id);
                    table.CheckConstraint("CK_Returns_DetailsJson_IsJson", "ISJSON([DetailsJson]) = 1");
                    table.ForeignKey(
                        name: "FK_Returns_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Returns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "RoleIdsJson", "RowVersion", "WishlistJson" },
                values: new object[] { "[]", null, "[]" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "RoleIdsJson", "RowVersion", "WishlistJson" },
                values: new object[] { "[]", null, "[]" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_RoleIdsJson_IsJson",
                table: "Users",
                sql: "ISJSON([RoleIdsJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_WishlistJson_IsJson",
                table: "Users",
                sql: "ISJSON([WishlistJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_PermissionsJson_IsJson",
                table: "Roles",
                sql: "ISJSON([PermissionsJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_MetadataJson_IsJson",
                table: "Reviews",
                sql: "ISJSON([MetadataJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ImagesJson_IsJson",
                table: "Products",
                sql: "ISJSON([ImagesJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_TagsJson_IsJson",
                table: "Products",
                sql: "ISJSON([TagsJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_NotesJson_IsJson",
                table: "Orders",
                sql: "ISJSON([NotesJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_StatusHistoryJson_IsJson",
                table: "Orders",
                sql: "ISJSON([StatusHistoryJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatSessions_MessagesJson_IsJson",
                table: "ChatSessions",
                sql: "ISJSON([MessagesJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Carts_LinesJson_IsJson",
                table: "Carts",
                sql: "ISJSON([LinesJson]) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ChangedAt",
                table: "AuditLogs",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ChangedByAdminId",
                table: "AuditLogs",
                column: "ChangedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_EntryType",
                table: "ContentEntries",
                column: "EntryType");

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_EntryType_Key",
                table: "ContentEntries",
                columns: new[] { "EntryType", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_IsActive",
                table: "ContentEntries",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntries_IsRead",
                table: "ContentEntries",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedAtUtc",
                table: "Payments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderTransactionId",
                table: "Payments",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Code",
                table: "Promotions",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_IsActive_StartsAt_EndsAt",
                table: "Promotions",
                columns: new[] { "IsActive", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Type",
                table: "Promotions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Returns_OrderId",
                table: "Returns",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Returns_ReturnNumber",
                table: "Returns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Returns_Status_ClaimDeadlineAtUtc",
                table: "Returns",
                columns: new[] { "Status", "ClaimDeadlineAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Returns_UserId_SubmittedAtUtc",
                table: "Returns",
                columns: new[] { "UserId", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ContentEntries");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "Returns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_RoleIdsJson_IsJson",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_WishlistJson_IsJson",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_PermissionsJson_IsJson",
                table: "Roles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_MetadataJson_IsJson",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ImagesJson_IsJson",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_TagsJson_IsJson",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_NotesJson_IsJson",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_StatusHistoryJson_IsJson",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatSessions_MessagesJson_IsJson",
                table: "ChatSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Carts_LinesJson_IsJson",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "RoleIdsJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WishlistJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PermissionsJson",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ImagesJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NotesJson",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StatusHistoryJson",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MessagesJson",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "LinesJson",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Carts");
        }
    }
}
