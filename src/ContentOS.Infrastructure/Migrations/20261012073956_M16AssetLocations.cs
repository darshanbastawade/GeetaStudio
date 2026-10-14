using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M16AssetLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetLocations",
                schema: "runtime",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VerifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetLocations", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_AssetLocations_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "runtime",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetLocations",
                schema: "runtime");
        }
    }
}
