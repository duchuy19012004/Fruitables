using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessages_AttemptCount", "[AttemptCount] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_IdempotencyKey",
                table: "OutboxMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending_NextAttemptAtUtc",
                table: "OutboxMessages",
                columns: new[] { "NextAttemptAtUtc", "OccurredAtUtc" },
                filter: "[ProcessedAtUtc] IS NULL AND [DeadLetteredAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
