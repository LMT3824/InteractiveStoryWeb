using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class FixChoiceRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Choices_ChapterSegments_NextSegmentId",
                table: "Choices");

            migrationBuilder.AddForeignKey(
                name: "FK_Choices_ChapterSegments_NextSegmentId",
                table: "Choices",
                column: "NextSegmentId",
                principalTable: "ChapterSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Choices_ChapterSegments_NextSegmentId",
                table: "Choices");

            migrationBuilder.AddForeignKey(
                name: "FK_Choices_ChapterSegments_NextSegmentId",
                table: "Choices",
                column: "NextSegmentId",
                principalTable: "ChapterSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
