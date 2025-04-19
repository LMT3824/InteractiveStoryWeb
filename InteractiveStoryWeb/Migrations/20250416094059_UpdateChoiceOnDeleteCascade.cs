using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChoiceOnDeleteCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Choices_ChapterSegments_ChapterSegmentId",
                table: "Choices");

            migrationBuilder.AddForeignKey(
                name: "FK_Choices_ChapterSegments_ChapterSegmentId",
                table: "Choices",
                column: "ChapterSegmentId",
                principalTable: "ChapterSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Choices_ChapterSegments_ChapterSegmentId",
                table: "Choices");

            migrationBuilder.AddForeignKey(
                name: "FK_Choices_ChapterSegments_ChapterSegmentId",
                table: "Choices",
                column: "ChapterSegmentId",
                principalTable: "ChapterSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
