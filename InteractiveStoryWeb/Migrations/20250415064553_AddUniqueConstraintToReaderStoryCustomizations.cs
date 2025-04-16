using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintToReaderStoryCustomizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReaderStoryCustomizations_UserId",
                table: "ReaderStoryCustomizations");

            migrationBuilder.CreateIndex(
                name: "IX_ReaderStoryCustomizations_UserId_StoryId",
                table: "ReaderStoryCustomizations",
                columns: new[] { "UserId", "StoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReaderStoryCustomizations_UserId_StoryId",
                table: "ReaderStoryCustomizations");

            migrationBuilder.CreateIndex(
                name: "IX_ReaderStoryCustomizations_UserId",
                table: "ReaderStoryCustomizations",
                column: "UserId");
        }
    }
}
