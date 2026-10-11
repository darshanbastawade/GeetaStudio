using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M5Evidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "evidence");

            migrationBuilder.CreateTable(
                name: "EvidenceClaim",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceClaim_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceCommand",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceCommand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceRubric",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MinimumIndependentSources = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceRubric", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceRubric_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimRevision",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tradition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Uncertainty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RubricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quality = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRevision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimRevision_EvidenceClaim_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "evidence",
                        principalTable: "EvidenceClaim",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimRevision_EvidenceRubric_RubricId",
                        column: x => x.RubricId,
                        principalSchema: "evidence",
                        principalTable: "EvidenceRubric",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimContentLink",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Start = table.Column<int>(type: "int", nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimContentLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimContentLink_ClaimRevision_ClaimRevisionId",
                        column: x => x.ClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimContentLink_Revisions_ContentRevisionId",
                        column: x => x.ContentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimCorrection",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReplacementRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimCorrection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimCorrection_ClaimRevision_ClaimRevisionId",
                        column: x => x.ClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimCorrection_ClaimRevision_ReplacementRevisionId",
                        column: x => x.ReplacementRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimDisagreement",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeftRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RightRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDisagreement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimDisagreement_ClaimRevision_LeftRevisionId",
                        column: x => x.LeftRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimDisagreement_ClaimRevision_RightRevisionId",
                        column: x => x.RightRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimEvidenceSpan",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorpusTextId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Start = table.Column<int>(type: "int", nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false),
                    ExactText = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locator = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Relation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimEvidenceSpan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimEvidenceSpan_ClaimRevision_ClaimRevisionId",
                        column: x => x.ClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimEvidenceSpan_CorpusText_CorpusTextId",
                        column: x => x.CorpusTextId,
                        principalSchema: "corpus",
                        principalTable: "CorpusText",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimEvidenceSpan_SourceSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "sources",
                        principalTable: "SourceSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimContentLink_ClaimRevisionId",
                schema: "evidence",
                table: "ClaimContentLink",
                column: "ClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimContentLink_ContentRevisionId_Start",
                schema: "evidence",
                table: "ClaimContentLink",
                columns: new[] { "ContentRevisionId", "Start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimCorrection_ClaimRevisionId",
                schema: "evidence",
                table: "ClaimCorrection",
                column: "ClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimCorrection_ReplacementRevisionId",
                schema: "evidence",
                table: "ClaimCorrection",
                column: "ReplacementRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDisagreement_LeftRevisionId",
                schema: "evidence",
                table: "ClaimDisagreement",
                column: "LeftRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDisagreement_RightRevisionId",
                schema: "evidence",
                table: "ClaimDisagreement",
                column: "RightRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimEvidenceSpan_ClaimRevisionId",
                schema: "evidence",
                table: "ClaimEvidenceSpan",
                column: "ClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimEvidenceSpan_CorpusTextId",
                schema: "evidence",
                table: "ClaimEvidenceSpan",
                column: "CorpusTextId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimEvidenceSpan_SnapshotId",
                schema: "evidence",
                table: "ClaimEvidenceSpan",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRevision_ClaimId_Version",
                schema: "evidence",
                table: "ClaimRevision",
                columns: new[] { "ClaimId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRevision_RubricId",
                schema: "evidence",
                table: "ClaimRevision",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceClaim_UniverseId_Key",
                schema: "evidence",
                table: "EvidenceClaim",
                columns: new[] { "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceClaim_UniverseId_TenantId",
                schema: "evidence",
                table: "EvidenceClaim",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceCommand_TenantId_OperationId",
                schema: "evidence",
                table: "EvidenceCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRubric_UniverseId_Key_Version",
                schema: "evidence",
                table: "EvidenceRubric",
                columns: new[] { "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRubric_UniverseId_TenantId",
                schema: "evidence",
                table: "EvidenceRubric",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimContentLink",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "ClaimCorrection",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "ClaimDisagreement",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "ClaimEvidenceSpan",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "EvidenceCommand",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "ClaimRevision",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "EvidenceClaim",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "EvidenceRubric",
                schema: "evidence");
        }
    }
}
