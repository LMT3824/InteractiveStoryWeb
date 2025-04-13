using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestampsToModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Stories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Choices",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Choices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ChapterSegments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ChapterSegments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChapterSegments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Chapters",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Choices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Choices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ChapterSegments");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ChapterSegments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ChapterSegments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Chapters");
        }
    }
}
