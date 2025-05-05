using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorToReportModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Blocks_Stories_BlockedStoryId",
                table: "Blocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Stories_StoryId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Libraries_Stories_StoryId",
                table: "Libraries");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Stories_StoryId",
                table: "Ratings");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Stories_StoryId",
                table: "Reports");

            migrationBuilder.AddColumn<string>(
                name: "AuthorId",
                table: "Reports",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AuthorId",
                table: "Reports",
                column: "AuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Blocks_Stories_BlockedStoryId",
                table: "Blocks",
                column: "BlockedStoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Stories_StoryId",
                table: "Comments",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Libraries_Stories_StoryId",
                table: "Libraries",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Stories_StoryId",
                table: "Ratings",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_AspNetUsers_AuthorId",
                table: "Reports",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Stories_StoryId",
                table: "Reports",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Blocks_Stories_BlockedStoryId",
                table: "Blocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Stories_StoryId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Libraries_Stories_StoryId",
                table: "Libraries");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Stories_StoryId",
                table: "Ratings");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_AspNetUsers_AuthorId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Stories_StoryId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_AuthorId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "Reports");

            migrationBuilder.AddForeignKey(
                name: "FK_Blocks_Stories_BlockedStoryId",
                table: "Blocks",
                column: "BlockedStoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Stories_StoryId",
                table: "Comments",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Libraries_Stories_StoryId",
                table: "Libraries",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Stories_StoryId",
                table: "Ratings",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Stories_StoryId",
                table: "Reports",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
