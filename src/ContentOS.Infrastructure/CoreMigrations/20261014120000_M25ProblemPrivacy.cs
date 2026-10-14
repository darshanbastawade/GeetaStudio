using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ContentOS.Infrastructure.CoreMigrations;

[DbContext(typeof(CoreDbContext))]
[Migration("20261014120000_M25ProblemPrivacy")]
public sealed class M25ProblemPrivacy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Original identifiers/fingerprints remain; obsolete copied diagnostic passages do not.
        migrationBuilder.Sql("UPDATE [talk].[TalkProblem] SET [EvidenceJson] = JSON_MODIFY([EvidenceJson], '$.sourcesJson', N'[]') WHERE ISJSON([EvidenceJson]) = 1");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new InvalidOperationException("Privacy redaction cannot be reversed. Restore/forward-fix must reapply M25 privacy policy before serving data.");
}
