using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ContentOS.Infrastructure.CoreMigrations;

[DbContext(typeof(CoreDbContext))]
[Migration("20261014141000_M27DispatchPrivacy")]
public sealed class M27DispatchPrivacy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep identity, exact replay fingerprint and reserved operations. Remove prototype fixture inputs.
        migrationBuilder.Sql("""
UPDATE [optimization].[OptimizationEvent]
SET [EvidenceJson] = (SELECT [OperationId] AS [operationId], [Fingerprint] AS [requestHash],
    N'NoRawGenerationInputs' AS [inputRetention] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
    [ResultJson] = JSON_MODIFY(JSON_MODIFY([ResultJson], '$.primaryPrompt', NULL), '$.shadowPrompt', NULL)
WHERE [Kind] = N'dispatch' AND JSON_VALUE([EvidenceJson], '$.inputRetention') IS NULL;
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Redacted generation inputs cannot be recreated; use a reviewed forward fix.");
}
