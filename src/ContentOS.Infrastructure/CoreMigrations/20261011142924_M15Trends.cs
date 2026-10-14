using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M15Trends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trends");

            migrationBuilder.CreateTable(
                name: "TrendCluster",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Dilemma = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CheckedFingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendCluster", x => x.Id);
                    table.UniqueConstraint("AK_TrendCluster_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendCluster_ClaimRevision_ClaimRevisionId",
                        column: x => x.ClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendCluster_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrendConnector",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendConnector", x => x.Id);
                    table.UniqueConstraint("AK_TrendConnector_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendConnector_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrendOperation",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendOperation", x => x.Id);
                    table.UniqueConstraint("AK_TrendOperation_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendOperation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrendTeaching",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendTeaching", x => x.Id);
                    table.UniqueConstraint("AK_TrendTeaching_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendTeaching_LessonVariant_LessonVariantId",
                        column: x => x.LessonVariantId,
                        principalSchema: "lessons",
                        principalTable: "LessonVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendTeaching_TrendCluster_ClusterId_UniverseId_TenantId",
                        columns: x => new { x.ClusterId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendCluster",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendTeaching_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrendCandidate",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtractionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetrievedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendCandidate", x => x.Id);
                    table.UniqueConstraint("AK_TrendCandidate_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendCandidate_TrendCluster_ClusterId_UniverseId_TenantId",
                        columns: x => new { x.ClusterId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendCluster",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendCandidate_TrendConnector_ConnectorId_UniverseId_TenantId",
                        columns: x => new { x.ConnectorId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendConnector",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendCandidate_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrendStoryBrief",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeachingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FactoryPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendStoryBrief", x => x.Id);
                    table.UniqueConstraint("AK_TrendStoryBrief_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendStoryBrief_FactoryPlan_FactoryPlanId",
                        column: x => x.FactoryPlanId,
                        principalSchema: "factory",
                        principalTable: "FactoryPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendStoryBrief_TrendCluster_ClusterId_UniverseId_TenantId",
                        columns: x => new { x.ClusterId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendCluster",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendStoryBrief_TrendTeaching_TeachingId_UniverseId_TenantId",
                        columns: x => new { x.TeachingId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendTeaching",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendStoryBrief_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrendBinding",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendBinding", x => x.Id);
                    table.UniqueConstraint("AK_TrendBinding_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendBinding_Revisions_ContentRevisionId",
                        column: x => x.ContentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendBinding_TrendStoryBrief_BriefId_UniverseId_TenantId",
                        columns: x => new { x.BriefId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendStoryBrief",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendBinding_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrendRemediation",
                schema: "trends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EditorialCorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendRemediation", x => x.Id);
                    table.UniqueConstraint("AK_TrendRemediation_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TrendRemediation_TrendCluster_ClusterId_UniverseId_TenantId",
                        columns: x => new { x.ClusterId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendCluster",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendRemediation_TrendStoryBrief_BriefId_UniverseId_TenantId",
                        columns: x => new { x.BriefId, x.UniverseId, x.TenantId },
                        principalSchema: "trends",
                        principalTable: "TrendStoryBrief",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrendRemediation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrendBinding_BriefId_ContentRevisionId",
                schema: "trends",
                table: "TrendBinding",
                columns: new[] { "BriefId", "ContentRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrendBinding_BriefId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendBinding",
                columns: new[] { "BriefId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendBinding_ContentRevisionId",
                schema: "trends",
                table: "TrendBinding",
                column: "ContentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrendBinding_UniverseId_TenantId",
                schema: "trends",
                table: "TrendBinding",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendCandidate_ClusterId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendCandidate",
                columns: new[] { "ClusterId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendCandidate_ConnectorId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendCandidate",
                columns: new[] { "ConnectorId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendCandidate_UniverseId_TenantId",
                schema: "trends",
                table: "TrendCandidate",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendCluster_ClaimRevisionId",
                schema: "trends",
                table: "TrendCluster",
                column: "ClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrendCluster_TenantId_UniverseId_Key",
                schema: "trends",
                table: "TrendCluster",
                columns: new[] { "TenantId", "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrendCluster_UniverseId_TenantId",
                schema: "trends",
                table: "TrendCluster",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendConnector_TenantId_UniverseId_Key_Version",
                schema: "trends",
                table: "TrendConnector",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrendConnector_UniverseId_TenantId",
                schema: "trends",
                table: "TrendConnector",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendOperation_TenantId_OperationId",
                schema: "trends",
                table: "TrendOperation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrendOperation_UniverseId_TenantId",
                schema: "trends",
                table: "TrendOperation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendRemediation_BriefId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendRemediation",
                columns: new[] { "BriefId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendRemediation_ClusterId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendRemediation",
                columns: new[] { "ClusterId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendRemediation_UniverseId_TenantId",
                schema: "trends",
                table: "TrendRemediation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendStoryBrief_ClusterId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendStoryBrief",
                columns: new[] { "ClusterId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendStoryBrief_FactoryPlanId",
                schema: "trends",
                table: "TrendStoryBrief",
                column: "FactoryPlanId",
                unique: true,
                filter: "[FactoryPlanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrendStoryBrief_TeachingId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendStoryBrief",
                columns: new[] { "TeachingId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendStoryBrief_TenantId_UniverseId_Key_Version",
                schema: "trends",
                table: "TrendStoryBrief",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrendStoryBrief_UniverseId_TenantId",
                schema: "trends",
                table: "TrendStoryBrief",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendTeaching_ClusterId_UniverseId_TenantId",
                schema: "trends",
                table: "TrendTeaching",
                columns: new[] { "ClusterId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendTeaching_LessonVariantId",
                schema: "trends",
                table: "TrendTeaching",
                column: "LessonVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_TrendTeaching_UniverseId_TenantId",
                schema: "trends",
                table: "TrendTeaching",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrendBinding",
                schema: "trends");

            migrationBuilder.DropTable(
                name: "TrendCandidate",
                schema: "trends");

            migrationBuilder.DropTable(
                name: "TrendOperation",
                schema: "trends");

            migrationBuilder.DropTable(
                name: "TrendRemediation",
                schema: "trends");

            migrationBuilder.DropTable(
                name: "TrendConnector",
                schema: "trends");

            migrationBuilder.DropTable(
                name: "TrendStoryBrief",
                schema: "trends");

            migrationBuilder.DropTable(
                name: "TrendTeaching",
                schema: "trends");

            migrationBuilder.DropTable(
                name: "TrendCluster",
                schema: "trends");
        }
    }
}
