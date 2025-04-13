using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveStoryWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionAndViewCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "Stories");

            migrationBuilder.AlterColumn<string>(
                name: "Genre",
                table: "Stories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Stories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "Chapters",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "Chapters");

            migrationBuilder.AlterColumn<string>(
                name: "Genre",
                table: "Stories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "Stories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
