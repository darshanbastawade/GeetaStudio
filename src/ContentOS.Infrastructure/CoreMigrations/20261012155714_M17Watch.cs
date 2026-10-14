using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M17Watch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "watch");

            migrationBuilder.CreateTable(
                name: "WatchOperation",
                schema: "watch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchOperation", x => x.Id);
                    table.UniqueConstraint("AK_WatchOperation_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_WatchOperation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchProfile",
                schema: "watch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchProfile", x => x.Id);
                    table.UniqueConstraint("AK_WatchProfile_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_WatchProfile_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchTemplate",
                schema: "watch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchTemplate", x => x.Id);
                    table.UniqueConstraint("AK_WatchTemplate_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_WatchTemplate_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchProduction",
                schema: "watch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoreRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    TimelineJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    ReviewMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchProduction", x => x.Id);
                    table.UniqueConstraint("AK_WatchProduction_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_WatchProduction_Revisions_CoreRevisionId",
                        column: x => x.CoreRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchProduction_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchProduction_WatchProfile_ProfileId_UniverseId_TenantId",
                        columns: x => new { x.ProfileId, x.UniverseId, x.TenantId },
                        principalSchema: "watch",
                        principalTable: "WatchProfile",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchStage",
                schema: "watch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    ExternalResourceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchStage", x => x.Id);
                    table.UniqueConstraint("AK_WatchStage_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_WatchStage_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchStage_WatchProduction_ProductionId_UniverseId_TenantId",
                        columns: x => new { x.ProductionId, x.UniverseId, x.TenantId },
                        principalSchema: "watch",
                        principalTable: "WatchProduction",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchArtifact",
                schema: "watch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aspect = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchArtifact", x => x.Id);
                    table.UniqueConstraint("AK_WatchArtifact_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_WatchArtifact_Assets_AssetReferenceId",
                        column: x => x.AssetReferenceId,
                        principalSchema: "core",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchArtifact_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchArtifact_WatchProduction_ProductionId_UniverseId_TenantId",
                        columns: x => new { x.ProductionId, x.UniverseId, x.TenantId },
                        principalSchema: "watch",
                        principalTable: "WatchProduction",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchArtifact_WatchStage_StageId_UniverseId_TenantId",
                        columns: x => new { x.StageId, x.UniverseId, x.TenantId },
                        principalSchema: "watch",
                        principalTable: "WatchStage",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchArtifact_AssetReferenceId",
                schema: "watch",
                table: "WatchArtifact",
                column: "AssetReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchArtifact_ProductionId_UniverseId_TenantId",
                schema: "watch",
                table: "WatchArtifact",
                columns: new[] { "ProductionId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchArtifact_StageId",
                schema: "watch",
                table: "WatchArtifact",
                column: "StageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchArtifact_StageId_UniverseId_TenantId",
                schema: "watch",
                table: "WatchArtifact",
                columns: new[] { "StageId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchArtifact_UniverseId_TenantId",
                schema: "watch",
                table: "WatchArtifact",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchOperation_TenantId_OperationId",
                schema: "watch",
                table: "WatchOperation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchOperation_UniverseId_TenantId",
                schema: "watch",
                table: "WatchOperation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchProduction_CoreRevisionId",
                schema: "watch",
                table: "WatchProduction",
                column: "CoreRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchProduction_ProfileId_UniverseId_TenantId",
                schema: "watch",
                table: "WatchProduction",
                columns: new[] { "ProfileId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchProduction_TenantId_UniverseId_Key_Version",
                schema: "watch",
                table: "WatchProduction",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchProduction_UniverseId_TenantId",
                schema: "watch",
                table: "WatchProduction",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchProfile_TenantId_UniverseId_Key_Version",
                schema: "watch",
                table: "WatchProfile",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchProfile_UniverseId_TenantId",
                schema: "watch",
                table: "WatchProfile",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchStage_ProductionId_Sequence",
                schema: "watch",
                table: "WatchStage",
                columns: new[] { "ProductionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchStage_ProductionId_UniverseId_TenantId",
                schema: "watch",
                table: "WatchStage",
                columns: new[] { "ProductionId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchStage_State_NextUtc_LeaseUntilUtc",
                schema: "watch",
                table: "WatchStage",
                columns: new[] { "State", "NextUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchStage_UniverseId_TenantId",
                schema: "watch",
                table: "WatchStage",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchTemplate_TenantId_UniverseId_Key_Version",
                schema: "watch",
                table: "WatchTemplate",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchTemplate_UniverseId_TenantId",
                schema: "watch",
                table: "WatchTemplate",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchArtifact",
                schema: "watch");

            migrationBuilder.DropTable(
                name: "WatchOperation",
                schema: "watch");

            migrationBuilder.DropTable(
                name: "WatchTemplate",
                schema: "watch");

            migrationBuilder.DropTable(
                name: "WatchStage",
                schema: "watch");

            migrationBuilder.DropTable(
                name: "WatchProduction",
                schema: "watch");

            migrationBuilder.DropTable(
                name: "WatchProfile",
                schema: "watch");
        }
    }
}
