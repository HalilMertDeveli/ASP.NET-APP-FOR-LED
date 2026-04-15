using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPanelSupportFileIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PValue",
                table: "PanelSupportFiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FileType",
                table: "PanelSupportFiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DecoderValue",
                table: "PanelSupportFiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ChipsetValue",
                table: "PanelSupportFiles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_PanelSupportFiles_PValue_ChipsetValue_DecoderValue",
                table: "PanelSupportFiles",
                columns: new[] { "PValue", "ChipsetValue", "DecoderValue" });

            migrationBuilder.CreateIndex(
                name: "IX_PanelSupportFiles_PValue_ChipsetValue_DecoderValue_FileType",
                table: "PanelSupportFiles",
                columns: new[] { "PValue", "ChipsetValue", "DecoderValue", "FileType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PanelSupportFiles_PValue_ChipsetValue_DecoderValue",
                table: "PanelSupportFiles");

            migrationBuilder.DropIndex(
                name: "IX_PanelSupportFiles_PValue_ChipsetValue_DecoderValue_FileType",
                table: "PanelSupportFiles");

            migrationBuilder.AlterColumn<string>(
                name: "PValue",
                table: "PanelSupportFiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FileType",
                table: "PanelSupportFiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DecoderValue",
                table: "PanelSupportFiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ChipsetValue",
                table: "PanelSupportFiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
