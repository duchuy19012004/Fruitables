using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class MakeReturnRowVersionApplicationManaged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersionNew",
                table: "ReturnRequests",
                type: "varbinary(16)",
                nullable: true);

            migrationBuilder.Sql("UPDATE [ReturnRequests] SET [RowVersionNew] = CONVERT(varbinary(16), [RowVersion]);");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReturnRequests");

            migrationBuilder.RenameColumn(
                name: "RowVersionNew",
                table: "ReturnRequests",
                newName: "RowVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReturnRequests");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReturnRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }
    }
}
