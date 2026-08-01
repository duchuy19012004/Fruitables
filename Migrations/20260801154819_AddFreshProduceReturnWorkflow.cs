using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddFreshProduceReturnWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedReviewerId",
                table: "ReturnRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecisionPackageVersion",
                table: "ReturnRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionProposedAtUtc",
                table: "ReturnRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManagerApprovalDueAtUtc",
                table: "ReturnRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewSlaHoursSnapshot",
                table: "ReturnRequests",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<int>(
                name: "AppealCount",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AppealDeadlineAtUtc",
                table: "ReturnRequestItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cause",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CostBearer",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "CurrentDecisionProposalId",
                table: "ReturnRequestItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DamagePercentageApproved",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DamagePercentageRequested",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "ReturnRequestItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AllowedDamagePercentages",
                table: "ReturnPolicies",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "AppealWindowHours",
                table: "ReturnPolicies",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<decimal>(
                name: "AutoApprovalAmountCap",
                table: "ReturnPolicies",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 100000m);

            migrationBuilder.AddColumn<bool>(
                name: "AutoApprovalEnabled",
                table: "ReturnPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AutoApprovalOrderRatioCap",
                table: "ReturnPolicies",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 30m);

            migrationBuilder.AddColumn<decimal>(
                name: "PostReviewSampleRate",
                table: "ReturnPolicies",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReturnPolicies",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplementWindowHours",
                table: "ReturnPolicies",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<int>(
                name: "ReturnDecisionProposalId",
                table: "ReturnEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnRequestItemId",
                table: "ReturnEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailureAttemptCount",
                table: "Refunds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FailureKind",
                table: "Refunds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancialSeparationThresholdSnapshot",
                table: "Refunds",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFeeAmount",
                table: "Refunds",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityKg",
                table: "InventoryDispositions",
                type: "decimal(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ReturnAccountSupportRestrictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnAccountSupportRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnAccountSupportRestrictions_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnAccountSupportRestrictions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnAccountSupportRestrictions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnApprovalRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    RequiresDifferentActor = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnApprovalRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnApprovalRules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnDecisionProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ProposedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    AggregateAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ShippingFeeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ShippingFeeEligibilitySnapshot = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnDecisionProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnDecisionProposals_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnDecisionProposals_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnDecisionProposals_Users_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnEvidenceLinks",
                columns: table => new
                {
                    ReturnEvidenceId = table.Column<int>(type: "int", nullable: false),
                    ReturnRequestItemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnEvidenceLinks", x => new { x.ReturnEvidenceId, x.ReturnRequestItemId });
                    table.ForeignKey(
                        name: "FK_ReturnEvidenceLinks_ReturnEvidences_ReturnEvidenceId",
                        column: x => x.ReturnEvidenceId,
                        principalTable: "ReturnEvidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnEvidenceLinks_ReturnRequestItems_ReturnRequestItemId",
                        column: x => x.ReturnRequestItemId,
                        principalTable: "ReturnRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnDecisionProposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnDecisionProposalId = table.Column<int>(type: "int", nullable: false),
                    ReturnRequestItemId = table.Column<int>(type: "int", nullable: false),
                    ApprovedQuantity = table.Column<int>(type: "int", nullable: false),
                    ApprovedDamagePercentage = table.Column<int>(type: "int", nullable: false),
                    Cause = table.Column<int>(type: "int", nullable: false),
                    CostBearer = table.Column<int>(type: "int", nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnDecisionProposalItems", x => x.Id);
                    table.CheckConstraint("CK_ReturnDecisionProposalItems_ApprovedQuantity", "[ApprovedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_ReturnDecisionProposalItems_ReturnDecisionProposals_ReturnDecisionProposalId",
                        column: x => x.ReturnDecisionProposalId,
                        principalTable: "ReturnDecisionProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnDecisionProposalItems_ReturnRequestItems_ReturnRequestItemId",
                        column: x => x.ReturnRequestItemId,
                        principalTable: "ReturnRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_AssignedReviewerId",
                table: "ReturnRequests",
                column: "AssignedReviewerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReturnRequestItems_DamagePercentageApproved",
                table: "ReturnRequestItems",
                sql: "[DamagePercentageApproved] IN (0, 25, 50, 75, 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReturnRequestItems_DamagePercentageRequested",
                table: "ReturnRequestItems",
                sql: "[DamagePercentageRequested] IN (0, 25, 50, 75, 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReturnPolicies_AutoApprovalRatio",
                table: "ReturnPolicies",
                sql: "[AutoApprovalOrderRatioCap] >= 0 AND [AutoApprovalOrderRatioCap] <= 100");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvents_ReturnDecisionProposalId",
                table: "ReturnEvents",
                column: "ReturnDecisionProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvents_ReturnRequestItemId",
                table: "ReturnEvents",
                column: "ReturnRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnAccountSupportRestrictions_ApprovedByUserId",
                table: "ReturnAccountSupportRestrictions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnAccountSupportRestrictions_CreatedByUserId",
                table: "ReturnAccountSupportRestrictions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnAccountSupportRestrictions_UserId_EffectiveFromUtc_ExpiresAtUtc",
                table: "ReturnAccountSupportRestrictions",
                columns: new[] { "UserId", "EffectiveFromUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnApprovalRules_CreatedByUserId",
                table: "ReturnApprovalRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnApprovalRules_RoleName_Action_Version",
                table: "ReturnApprovalRules",
                columns: new[] { "RoleName", "Action", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnDecisionProposalItems_ReturnDecisionProposalId_ReturnRequestItemId",
                table: "ReturnDecisionProposalItems",
                columns: new[] { "ReturnDecisionProposalId", "ReturnRequestItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnDecisionProposalItems_ReturnRequestItemId",
                table: "ReturnDecisionProposalItems",
                column: "ReturnRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnDecisionProposals_ApprovedByUserId",
                table: "ReturnDecisionProposals",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnDecisionProposals_ProposedByUserId",
                table: "ReturnDecisionProposals",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnDecisionProposals_ReturnRequestId_Version",
                table: "ReturnDecisionProposals",
                columns: new[] { "ReturnRequestId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnEvidenceLinks_ReturnRequestItemId",
                table: "ReturnEvidenceLinks",
                column: "ReturnRequestItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnEvents_ReturnDecisionProposals_ReturnDecisionProposalId",
                table: "ReturnEvents",
                column: "ReturnDecisionProposalId",
                principalTable: "ReturnDecisionProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnEvents_ReturnRequestItems_ReturnRequestItemId",
                table: "ReturnEvents",
                column: "ReturnRequestItemId",
                principalTable: "ReturnRequestItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnRequests_Users_AssignedReviewerId",
                table: "ReturnRequests",
                column: "AssignedReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReturnEvents_ReturnDecisionProposals_ReturnDecisionProposalId",
                table: "ReturnEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnEvents_ReturnRequestItems_ReturnRequestItemId",
                table: "ReturnEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnRequests_Users_AssignedReviewerId",
                table: "ReturnRequests");

            migrationBuilder.DropTable(
                name: "ReturnAccountSupportRestrictions");

            migrationBuilder.DropTable(
                name: "ReturnApprovalRules");

            migrationBuilder.DropTable(
                name: "ReturnDecisionProposalItems");

            migrationBuilder.DropTable(
                name: "ReturnEvidenceLinks");

            migrationBuilder.DropTable(
                name: "ReturnDecisionProposals");

            migrationBuilder.DropIndex(
                name: "IX_ReturnRequests_AssignedReviewerId",
                table: "ReturnRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReturnRequestItems_DamagePercentageApproved",
                table: "ReturnRequestItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReturnRequestItems_DamagePercentageRequested",
                table: "ReturnRequestItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReturnPolicies_AutoApprovalRatio",
                table: "ReturnPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ReturnEvents_ReturnDecisionProposalId",
                table: "ReturnEvents");

            migrationBuilder.DropIndex(
                name: "IX_ReturnEvents_ReturnRequestItemId",
                table: "ReturnEvents");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerId",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "DecisionPackageVersion",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "DecisionProposedAtUtc",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "ManagerApprovalDueAtUtc",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "ReviewSlaHoursSnapshot",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "AppealCount",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "AppealDeadlineAtUtc",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "Cause",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "CostBearer",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "CurrentDecisionProposalId",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "DamagePercentageApproved",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "DamagePercentageRequested",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "AllowedDamagePercentages",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "AppealWindowHours",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "AutoApprovalAmountCap",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "AutoApprovalEnabled",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "AutoApprovalOrderRatioCap",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "PostReviewSampleRate",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "SupplementWindowHours",
                table: "ReturnPolicies");

            migrationBuilder.DropColumn(
                name: "ReturnDecisionProposalId",
                table: "ReturnEvents");

            migrationBuilder.DropColumn(
                name: "ReturnRequestItemId",
                table: "ReturnEvents");

            migrationBuilder.DropColumn(
                name: "FailureAttemptCount",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "FailureKind",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "FinancialSeparationThresholdSnapshot",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ShippingFeeAmount",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "QuantityKg",
                table: "InventoryDispositions");
        }
    }
}
