using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ContentOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M1ProviderDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Definitions",
                schema: "runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Capability = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContractVersion = table.Column<int>(type: "int", nullable: false),
                    ApprovedEndpoint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SupportsIdempotency = table.Column<bool>(type: "bit", nullable: false),
                    SupportsReconciliation = table.Column<bool>(type: "bit", nullable: false),
                    Qualification = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Definitions", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "runtime",
                table: "Definitions",
                columns: new[] { "Id", "ApprovedEndpoint", "Capability", "ContractVersion", "Qualification", "SupportsIdempotency", "SupportsReconciliation" },
                values: new object[,]
                {
                    { "test.a", "local:test", "runtime.probe", 1, "DeterministicTestOnly", true, true },
                    { "test.b", "local:test", "runtime.probe", 1, "DeterministicTestOnly", true, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_AdapterKey",
                schema: "runtime",
                table: "Connections",
                column: "AdapterKey");

            migrationBuilder.AddForeignKey(
                name: "FK_Connections_Definitions_AdapterKey",
                schema: "runtime",
                table: "Connections",
                column: "AdapterKey",
                principalSchema: "runtime",
                principalTable: "Definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Connections_Definitions_AdapterKey",
                schema: "runtime",
                table: "Connections");

            migrationBuilder.DropTable(
                name: "Definitions",
                schema: "runtime");

            migrationBuilder.DropIndex(
                name: "IX_Connections_AdapterKey",
                schema: "runtime",
                table: "Connections");
        }
    }
}
