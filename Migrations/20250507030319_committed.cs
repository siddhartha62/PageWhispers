using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PageWhispers.Migrations
{
    /// <inheritdoc />
    public partial class committed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Books",
                newName: "BookDescription");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BookDescription",
                table: "Books",
                newName: "Description");
        }
    }
}
