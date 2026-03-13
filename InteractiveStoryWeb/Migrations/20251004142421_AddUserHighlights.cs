using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHighlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kiểm tra và xóa Foreign Key nếu tồn tại
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Comments_Chapters_ChapterId')
                BEGIN
                    ALTER TABLE Comments DROP CONSTRAINT FK_Comments_Chapters_ChapterId
                END
            ");

            // Kiểm tra và xóa Index nếu tồn tại
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Comments_ChapterId' AND object_id = OBJECT_ID('Comments'))
                BEGIN
                    DROP INDEX IX_Comments_ChapterId ON Comments
                END
            ");

            // Kiểm tra và xóa Column nếu tồn tại
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE Name = 'ChapterId' AND Object_ID = Object_ID('Comments'))
                BEGIN
                    ALTER TABLE Comments DROP COLUMN ChapterId
                END
            ");

            migrationBuilder.CreateTable(
                name: "UserHighlights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChapterSegmentId = table.Column<int>(type: "int", nullable: false),
                    HighlightedText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartOffset = table.Column<int>(type: "int", nullable: false),
                    EndOffset = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHighlights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserHighlights_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserHighlights_ChapterSegments_ChapterSegmentId",
                        column: x => x.ChapterSegmentId,
                        principalTable: "ChapterSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserHighlights_ChapterSegmentId",
                table: "UserHighlights",
                column: "ChapterSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserHighlights_UserId_ChapterSegmentId",
                table: "UserHighlights",
                columns: new[] { "UserId", "ChapterSegmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserHighlights");

            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "Comments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ChapterId",
                table: "Comments",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Chapters_ChapterId",
                table: "Comments",
                column: "ChapterId",
                principalTable: "Chapters",
                principalColumn: "Id");
        }
    }
}
