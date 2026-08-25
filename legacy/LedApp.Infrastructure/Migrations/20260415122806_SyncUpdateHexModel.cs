using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncUpdateHexModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UpdateHexFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionLabel = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateHexFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UpdateHexMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LookupKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VersionLabel = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateHexMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpdateHexFiles_VersionLabel",
                table: "UpdateHexFiles",
                column: "VersionLabel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UpdateHexMappings_LookupKey",
                table: "UpdateHexMappings",
                column: "LookupKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdateHexFiles");

            migrationBuilder.DropTable(
                name: "UpdateHexMappings");
        }
    }
}
