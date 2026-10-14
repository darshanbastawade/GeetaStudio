using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ContentOS.Infrastructure.CoreMigrations;

[DbContext(typeof(CoreDbContext))]
[Migration("20261014142000_M27DispatchPrivacyCaseFix")]
public sealed class M27DispatchPrivacyCaseFix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
UPDATE [optimization].[OptimizationEvent]
SET [ResultJson] = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY([ResultJson],
    '$.PrimaryPrompt', NULL), '$.ShadowPrompt', NULL), '$.primaryPrompt', NULL), '$.shadowPrompt', NULL)
WHERE [Kind] = N'dispatch';
""");
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Redacted generation inputs cannot be recreated; use a reviewed forward fix.");
}
