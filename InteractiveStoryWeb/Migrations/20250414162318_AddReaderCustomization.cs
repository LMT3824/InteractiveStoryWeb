using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowCustomization",
                table: "Stories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ReaderStoryCustomizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstPersonPronoun = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondPersonPronoun = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderStoryCustomizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReaderStoryCustomizations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReaderStoryCustomizations_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReaderStoryCustomizations_StoryId",
                table: "ReaderStoryCustomizations",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReaderStoryCustomizations_UserId",
                table: "ReaderStoryCustomizations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReaderStoryCustomizations");

            migrationBuilder.DropColumn(
                name: "AllowCustomization",
                table: "Stories");
        }
    }
}
