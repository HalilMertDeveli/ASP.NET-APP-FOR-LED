using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPanelSupportBinaryFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PanelSupportFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PanelType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChipsetValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecoderValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PanelSupportFiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PanelSupportFiles");
        }
    }
}
