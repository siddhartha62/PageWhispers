using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PageWhispers.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimCodeToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scent",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "Volume",
                table: "Books");

            migrationBuilder.AddColumn<string>(
                name: "ClaimCode",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimCode",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "Scent",
                table: "Books",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Volume",
                table: "Books",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
