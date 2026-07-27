using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundDestination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationAccountHolderProtected",
                table: "Refunds",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationAccountLast4",
                table: "Refunds",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationAccountNumberProtected",
                table: "Refunds",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationBankCode",
                table: "Refunds",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DestinationSubmittedAtUtc",
                table: "Refunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();

                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'CustomerSupport')
                    INSERT INTO Roles(Name, Description, IsActive, CreatedAt, UpdatedAt)
                    VALUES ('CustomerSupport', N'Nhân viên chăm sóc khách hàng', 1, @now, @now);

                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Finance')
                    INSERT INTO Roles(Name, Description, IsActive, CreatedAt, UpdatedAt)
                    VALUES ('Finance', N'Nhân viên tài chính xử lý hoàn tiền', 1, @now, @now);

                INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
                SELECT r.Id, p.Id, @now, NULL
                FROM Roles r CROSS JOIN Permissions p
                WHERE r.Name = 'CustomerSupport'
                  AND p.Name IN ('returns.view', 'returns.review', 'returns.approve', 'returns.reject')
                  AND NOT EXISTS (
                      SELECT 1 FROM RolePermissions rp
                      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
                  );

                INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
                SELECT r.Id, p.Id, @now, NULL
                FROM Roles r CROSS JOIN Permissions p
                WHERE r.Name = 'Finance' AND p.Name = 'returns.refund'
                  AND NOT EXISTS (
                      SELECT 1 FROM RolePermissions rp
                      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
                  );

                DELETE rp
                FROM RolePermissions rp
                INNER JOIN Roles r ON r.Id = rp.RoleId
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE r.Name = 'Admin' AND p.Name = 'returns.refund';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();

                INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
                SELECT r.Id, p.Id, @now, NULL
                FROM Roles r CROSS JOIN Permissions p
                WHERE r.Name = 'Admin' AND p.Name = 'returns.refund'
                  AND NOT EXISTS (
                      SELECT 1 FROM RolePermissions rp
                      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
                  );

                DELETE rp
                FROM RolePermissions rp
                INNER JOIN Roles r ON r.Id = rp.RoleId
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE (r.Name = 'CustomerSupport' AND p.Name = 'returns.approve')
                   OR (r.Name = 'Finance' AND p.Name = 'returns.refund');
                """);

            migrationBuilder.DropColumn(
                name: "DestinationAccountHolderProtected",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "DestinationAccountLast4",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "DestinationAccountNumberProtected",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "DestinationBankCode",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "DestinationSubmittedAtUtc",
                table: "Refunds");
        }
    }
}
