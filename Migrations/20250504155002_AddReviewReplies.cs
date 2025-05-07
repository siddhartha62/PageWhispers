using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PageWhispers.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Reviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentReviewBookId",
                table: "Reviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentReviewId",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentReviewUserId",
                table: "Reviews",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ParentReviewUserId_ParentReviewBookId",
                table: "Reviews",
                columns: new[] { "ParentReviewUserId", "ParentReviewBookId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Reviews_ParentReviewUserId_ParentReviewBookId",
                table: "Reviews",
                columns: new[] { "ParentReviewUserId", "ParentReviewBookId" },
                principalTable: "Reviews",
                principalColumns: new[] { "UserId", "BookId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Reviews_ParentReviewUserId_ParentReviewBookId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ParentReviewUserId_ParentReviewBookId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ParentReviewBookId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ParentReviewId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ParentReviewUserId",
                table: "Reviews");
        }
    }
}
