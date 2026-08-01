using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSentimentAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewSentiments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<int>(type: "int", nullable: false),
                    Sentiment = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    Confidence = table.Column<float>(type: "real", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    AnalyzedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminOverrideById = table.Column<int>(type: "int", nullable: true),
                    AdminOverrideAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AlertStatus = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedById = table.Column<int>(type: "int", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSentiments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSentiments_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewSentiments_Users_AcknowledgedById",
                        column: x => x.AcknowledgedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewSentiments_Users_AdminOverrideById",
                        column: x => x.AdminOverrideById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSentimentAspects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewSentimentId = table.Column<int>(type: "int", nullable: false),
                    Aspect = table.Column<int>(type: "int", nullable: false),
                    Sentiment = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSentimentAspects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSentimentAspects_ReviewSentiments_ReviewSentimentId",
                        column: x => x.ReviewSentimentId,
                        principalTable: "ReviewSentiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentimentAspects_ReviewSentimentId",
                table: "ReviewSentimentAspects",
                column: "ReviewSentimentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_AcknowledgedById",
                table: "ReviewSentiments",
                column: "AcknowledgedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_AdminOverrideById",
                table: "ReviewSentiments",
                column: "AdminOverrideById");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_AlertStatus",
                table: "ReviewSentiments",
                column: "AlertStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_ReviewId",
                table: "ReviewSentiments",
                column: "ReviewId",
                unique: true);

            migrationBuilder.Sql(@"
DECLARE @now datetime2 = SYSUTCDATETIME();
DECLARE @permission TABLE (Name nvarchar(100), Description nvarchar(500));
INSERT INTO @permission VALUES
('reviews.analyze',N'Xem phân tích cảm xúc review'),
('reviews.analyze_override',N'Sửa tay nhãn cảm xúc review'),
('reviews.analyze_trigger',N'Chạy backfill / phân tích lại review');
INSERT INTO Permissions (Name, Description, Module, CreatedAt)
SELECT p.Name,p.Description,'reviews',@now FROM @permission p
WHERE NOT EXISTS (SELECT 1 FROM Permissions x WHERE x.Name=p.Name);

INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='SuperAdmin' AND p.Name IN ('reviews.analyze','reviews.analyze_override','reviews.analyze_trigger')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='Admin' AND p.Name IN ('reviews.analyze','reviews.analyze_override','reviews.analyze_trigger')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='CustomerSupport' AND p.Name IN ('reviews.analyze')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_Sentiment",
                table: "ReviewSentiments",
                column: "Sentiment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewSentimentAspects");

            migrationBuilder.DropTable(
                name: "ReviewSentiments");
        }
    }
}
