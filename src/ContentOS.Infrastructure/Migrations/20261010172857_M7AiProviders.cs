using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ContentOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7AiProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "runtime",
                table: "Definitions",
                columns: new[] { "Id", "ApprovedEndpoint", "Capability", "ContractVersion", "Qualification", "SupportsIdempotency", "SupportsReconciliation" },
                values: new object[,]
                {
                    { "fixture.text.a", "local:ai-fixture", "ai.text", 1, "ProfileQualificationRequired", false, false },
                    { "fixture.text.b", "local:ai-fixture", "ai.text", 1, "ProfileQualificationRequired", false, false },
                    { "local.embedding.v1", "local:ai-fixture", "ai.embedding", 1, "ProfileQualificationRequired", false, false },
                    { "openai.text.v1", "approved:openai-chat-v1", "ai.text", 1, "ProfileQualificationRequired", false, false }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "runtime",
                table: "Definitions",
                keyColumn: "Id",
                keyValue: "fixture.text.a");

            migrationBuilder.DeleteData(
                schema: "runtime",
                table: "Definitions",
                keyColumn: "Id",
                keyValue: "fixture.text.b");

            migrationBuilder.DeleteData(
                schema: "runtime",
                table: "Definitions",
                keyColumn: "Id",
                keyValue: "local.embedding.v1");

            migrationBuilder.DeleteData(
                schema: "runtime",
                table: "Definitions",
                keyColumn: "Id",
                keyValue: "openai.text.v1");
        }
    }
}
