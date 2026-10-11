using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M6Knowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.CreateTable(
                name: "KnowledgeCommand",
                schema: "knowledge",
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
                    table.PrimaryKey("PK_KnowledgeCommand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeConcept",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TermsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeConcept", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeConcept_ClaimRevision_ClaimRevisionId",
                        column: x => x.ClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeConcept_SourceSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "sources",
                        principalTable: "SourceSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeConcept_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeIndex",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Territory = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Adapter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    Normalization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Chunking = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaterialJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    EmbeddingSpaceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GraphJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidationEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ValidationJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeIndex", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeIndex_CorpusManifest_ManifestId",
                        column: x => x.ManifestId,
                        principalSchema: "corpus",
                        principalTable: "CorpusManifest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeIndex_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeRelation",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeRelation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeRelation_ClaimRevision_ClaimRevisionId",
                        column: x => x.ClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeRelation_KnowledgeConcept_FromId",
                        column: x => x.FromId,
                        principalSchema: "knowledge",
                        principalTable: "KnowledgeConcept",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeRelation_KnowledgeConcept_ToId",
                        column: x => x.ToId,
                        principalSchema: "knowledge",
                        principalTable: "KnowledgeConcept",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeRelation_SourceSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "sources",
                        principalTable: "SourceSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeAlias",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActiveIndexId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousIndexId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeAlias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeAlias_KnowledgeIndex_ActiveIndexId",
                        column: x => x.ActiveIndexId,
                        principalSchema: "knowledge",
                        principalTable: "KnowledgeIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeAlias_KnowledgeIndex_PreviousIndexId",
                        column: x => x.PreviousIndexId,
                        principalSchema: "knowledge",
                        principalTable: "KnowledgeIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeAlias_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeChunk",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndexId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StableChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorpusTextId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VectorJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    FeaturesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeChunk", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeChunk_CorpusText_CorpusTextId",
                        column: x => x.CorpusTextId,
                        principalSchema: "corpus",
                        principalTable: "CorpusText",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeChunk_KnowledgeIndex_IndexId",
                        column: x => x.IndexId,
                        principalSchema: "knowledge",
                        principalTable: "KnowledgeIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeAlias_ActiveIndexId",
                schema: "knowledge",
                table: "KnowledgeAlias",
                column: "ActiveIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeAlias_PreviousIndexId",
                schema: "knowledge",
                table: "KnowledgeAlias",
                column: "PreviousIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeAlias_UniverseId_Locale_Key",
                schema: "knowledge",
                table: "KnowledgeAlias",
                columns: new[] { "UniverseId", "Locale", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunk_CorpusTextId",
                schema: "knowledge",
                table: "KnowledgeChunk",
                column: "CorpusTextId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunk_IndexId_StableChunkId",
                schema: "knowledge",
                table: "KnowledgeChunk",
                columns: new[] { "IndexId", "StableChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeCommand_TenantId_OperationId",
                schema: "knowledge",
                table: "KnowledgeCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeConcept_ClaimRevisionId",
                schema: "knowledge",
                table: "KnowledgeConcept",
                column: "ClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeConcept_SnapshotId",
                schema: "knowledge",
                table: "KnowledgeConcept",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeConcept_UniverseId_Key",
                schema: "knowledge",
                table: "KnowledgeConcept",
                columns: new[] { "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeConcept_UniverseId_TenantId",
                schema: "knowledge",
                table: "KnowledgeConcept",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeIndex_ManifestId",
                schema: "knowledge",
                table: "KnowledgeIndex",
                column: "ManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeIndex_State",
                schema: "knowledge",
                table: "KnowledgeIndex",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeIndex_UniverseId_Key",
                schema: "knowledge",
                table: "KnowledgeIndex",
                columns: new[] { "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeIndex_UniverseId_TenantId",
                schema: "knowledge",
                table: "KnowledgeIndex",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelation_ClaimRevisionId",
                schema: "knowledge",
                table: "KnowledgeRelation",
                column: "ClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelation_FromId",
                schema: "knowledge",
                table: "KnowledgeRelation",
                column: "FromId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelation_SnapshotId",
                schema: "knowledge",
                table: "KnowledgeRelation",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelation_ToId",
                schema: "knowledge",
                table: "KnowledgeRelation",
                column: "ToId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeAlias",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "KnowledgeChunk",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "KnowledgeCommand",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "KnowledgeRelation",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "KnowledgeIndex",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "KnowledgeConcept",
                schema: "knowledge");
        }
    }
}
