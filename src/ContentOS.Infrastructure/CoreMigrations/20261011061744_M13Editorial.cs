using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M13Editorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "editorial");

            migrationBuilder.CreateTable(
                name: "EditorialCorrectionRecord",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialCorrectionRecord", x => x.Id);
                    table.UniqueConstraint("AK_EditorialCorrectionRecord_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialCorrectionRecord_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialItem",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Brief = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublicIntended = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvaluationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvaluationCaseKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScheduledUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialItem", x => x.Id);
                    table.UniqueConstraint("AK_EditorialItem_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialItem_Contents_ContentId_UniverseId_TenantId",
                        columns: x => new { x.ContentId, x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Contents",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialItem_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialOperation",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialOperation", x => x.Id);
                    table.UniqueConstraint("AK_EditorialOperation_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialOperation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialTrend",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialTrend", x => x.Id);
                    table.UniqueConstraint("AK_EditorialTrend_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialTrend_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialApproval",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialApproval", x => x.Id);
                    table.UniqueConstraint("AK_EditorialApproval_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialApproval_EditorialItem_ItemId_UniverseId_TenantId",
                        columns: x => new { x.ItemId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialItem",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialApproval_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialApproval_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialAsset",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialAsset", x => x.Id);
                    table.UniqueConstraint("AK_EditorialAsset_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialAsset_Assets_AssetReferenceId",
                        column: x => x.AssetReferenceId,
                        principalSchema: "core",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialAsset_EditorialItem_ItemId_UniverseId_TenantId",
                        columns: x => new { x.ItemId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialItem",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialAsset_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialAsset_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialCopy",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialCopy", x => x.Id);
                    table.UniqueConstraint("AK_EditorialCopy_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialCopy_EditorialItem_ItemId_UniverseId_TenantId",
                        columns: x => new { x.ItemId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialItem",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialCopy_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialCopy_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialHistory",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialHistory", x => x.Id);
                    table.UniqueConstraint("AK_EditorialHistory_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialHistory_EditorialItem_ItemId_UniverseId_TenantId",
                        columns: x => new { x.ItemId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialItem",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialHistory_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EditorialCorrectionTask",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CopyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialCorrectionTask", x => x.Id);
                    table.UniqueConstraint("AK_EditorialCorrectionTask_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialCorrectionTask_Contents_ContentId_UniverseId_TenantId",
                        columns: x => new { x.ContentId, x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Contents",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialCorrectionTask_EditorialCopy_CopyId_UniverseId_TenantId",
                        columns: x => new { x.CopyId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialCopy",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialCorrectionTask_EditorialCorrectionRecord_CorrectionId_UniverseId_TenantId",
                        columns: x => new { x.CorrectionId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialCorrectionRecord",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialCorrectionTask_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialCorrectionTask_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialApproval_ItemId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialApproval",
                columns: new[] { "ItemId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialApproval_RevisionId",
                schema: "editorial",
                table: "EditorialApproval",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialApproval_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialApproval",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAsset_AssetReferenceId",
                schema: "editorial",
                table: "EditorialAsset",
                column: "AssetReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAsset_ItemId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialAsset",
                columns: new[] { "ItemId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAsset_RevisionId",
                schema: "editorial",
                table: "EditorialAsset",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAsset_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialAsset",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCopy_ItemId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialCopy",
                columns: new[] { "ItemId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCopy_RevisionId",
                schema: "editorial",
                table: "EditorialCopy",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCopy_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialCopy",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCorrectionRecord_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialCorrectionRecord",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCorrectionTask_ContentId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialCorrectionTask",
                columns: new[] { "ContentId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCorrectionTask_CopyId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialCorrectionTask",
                columns: new[] { "CopyId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCorrectionTask_CorrectionId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialCorrectionTask",
                columns: new[] { "CorrectionId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCorrectionTask_RevisionId",
                schema: "editorial",
                table: "EditorialCorrectionTask",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCorrectionTask_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialCorrectionTask",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialHistory_ItemId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialHistory",
                columns: new[] { "ItemId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialHistory_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialHistory",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialItem_ContentId",
                schema: "editorial",
                table: "EditorialItem",
                column: "ContentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialItem_ContentId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialItem",
                columns: new[] { "ContentId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialItem_TenantId_UniverseId_Key",
                schema: "editorial",
                table: "EditorialItem",
                columns: new[] { "TenantId", "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialItem_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialItem",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialOperation_TenantId_OperationId",
                schema: "editorial",
                table: "EditorialOperation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialOperation_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialOperation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialTrend_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialTrend",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditorialApproval",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialAsset",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialCorrectionTask",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialHistory",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialOperation",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialTrend",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialCopy",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialCorrectionRecord",
                schema: "editorial");

            migrationBuilder.DropTable(
                name: "EditorialItem",
                schema: "editorial");
        }
    }
}
