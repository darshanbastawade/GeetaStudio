using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M11Factory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "factory");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Revisions_Id_ContentId",
                schema: "core",
                table: "Revisions",
                columns: new[] { "Id", "ContentId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LessonVariant_Id_UniverseId_TenantId",
                schema: "lessons",
                table: "LessonVariant",
                columns: new[] { "Id", "UniverseId", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Contents_Id_UniverseId_TenantId",
                schema: "core",
                table: "Contents",
                columns: new[] { "Id", "UniverseId", "TenantId" });

            migrationBuilder.CreateTable(
                name: "FactoryOperation",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_FactoryOperation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactoryOperation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FactoryPlan",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryPlan", x => x.Id);
                    table.UniqueConstraint("AK_FactoryPlan_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_FactoryPlan_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FactoryArtifact",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LessonVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoreContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoreRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AudienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    Treatment = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryArtifact", x => x.Id);
                    table.UniqueConstraint("AK_FactoryArtifact_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_FactoryArtifact_Contents_CoreContentId_UniverseId_TenantId",
                        columns: x => new { x.CoreContentId, x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Contents",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryArtifact_FactoryArtifact_ParentArtifactId_UniverseId_TenantId",
                        columns: x => new { x.ParentArtifactId, x.UniverseId, x.TenantId },
                        principalSchema: "factory",
                        principalTable: "FactoryArtifact",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryArtifact_FactoryPlan_PlanId_UniverseId_TenantId",
                        columns: x => new { x.PlanId, x.UniverseId, x.TenantId },
                        principalSchema: "factory",
                        principalTable: "FactoryPlan",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryArtifact_LessonVariant_LessonVariantId_UniverseId_TenantId",
                        columns: x => new { x.LessonVariantId, x.UniverseId, x.TenantId },
                        principalSchema: "lessons",
                        principalTable: "LessonVariant",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryArtifact_Revisions_CoreRevisionId_CoreContentId",
                        columns: x => new { x.CoreRevisionId, x.CoreContentId },
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumns: new[] { "Id", "ContentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryArtifact_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FactoryBatch",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BudgetJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GeneratedCharacters = table.Column<int>(type: "int", nullable: false),
                    GenerationCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryBatch", x => x.Id);
                    table.UniqueConstraint("AK_FactoryBatch_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_FactoryBatch_FactoryPlan_PlanId_UniverseId_TenantId",
                        columns: x => new { x.PlanId, x.UniverseId, x.TenantId },
                        principalSchema: "factory",
                        principalTable: "FactoryPlan",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryBatch_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FactoryWork",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryWork", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactoryWork_FactoryArtifact_ArtifactId_UniverseId_TenantId",
                        columns: x => new { x.ArtifactId, x.UniverseId, x.TenantId },
                        principalSchema: "factory",
                        principalTable: "FactoryArtifact",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryWork_FactoryBatch_BatchId_UniverseId_TenantId",
                        columns: x => new { x.BatchId, x.UniverseId, x.TenantId },
                        principalSchema: "factory",
                        principalTable: "FactoryBatch",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactoryWork_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryArtifact_CoreContentId_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryArtifact",
                columns: new[] { "CoreContentId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryArtifact_CoreRevisionId_CoreContentId",
                schema: "factory",
                table: "FactoryArtifact",
                columns: new[] { "CoreRevisionId", "CoreContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryArtifact_LessonVariantId_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryArtifact",
                columns: new[] { "LessonVariantId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryArtifact_ParentArtifactId_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryArtifact",
                columns: new[] { "ParentArtifactId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryArtifact_PlanId_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryArtifact",
                columns: new[] { "PlanId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryArtifact_TenantId_UniverseId_Fingerprint",
                schema: "factory",
                table: "FactoryArtifact",
                columns: new[] { "TenantId", "UniverseId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryArtifact_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryArtifact",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryBatch_PlanId_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryBatch",
                columns: new[] { "PlanId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryBatch_TenantId_UniverseId_BusinessKey",
                schema: "factory",
                table: "FactoryBatch",
                columns: new[] { "TenantId", "UniverseId", "BusinessKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryBatch_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryBatch",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryOperation_TenantId_OperationId",
                schema: "factory",
                table: "FactoryOperation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryOperation_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryOperation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryPlan_TenantId_UniverseId_Key_Version",
                schema: "factory",
                table: "FactoryPlan",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryPlan_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryPlan",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryWork_ArtifactId_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryWork",
                columns: new[] { "ArtifactId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryWork_BatchId_BriefKey_Format",
                schema: "factory",
                table: "FactoryWork",
                columns: new[] { "BatchId", "BriefKey", "Format" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryWork_BatchId_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryWork",
                columns: new[] { "BatchId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryWork_UniverseId_TenantId",
                schema: "factory",
                table: "FactoryWork",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactoryOperation",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "FactoryWork",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "FactoryArtifact",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "FactoryBatch",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "FactoryPlan",
                schema: "factory");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Revisions_Id_ContentId",
                schema: "core",
                table: "Revisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LessonVariant_Id_UniverseId_TenantId",
                schema: "lessons",
                table: "LessonVariant");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Contents_Id_UniverseId_TenantId",
                schema: "core",
                table: "Contents");
        }
    }
}
