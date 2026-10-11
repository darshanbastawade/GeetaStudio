using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M3Sources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sources");

            migrationBuilder.CreateTable(
                name: "SourceAudit",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceCommand",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceCommand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceParty",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceParty", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceParty_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceUse",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Published = table.Column<bool>(type: "bit", nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceUse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceUse_Revisions_ContentRevisionId",
                        column: x => x.ContentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceWork",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Revoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceWork", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceWork_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermissionFormat",
                schema: "sources",
                columns: table => new
                {
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionFormat", x => new { x.PermissionId, x.Value });
                });

            migrationBuilder.CreateTable(
                name: "PermissionLanguage",
                schema: "sources",
                columns: table => new
                {
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionLanguage", x => new { x.PermissionId, x.Value });
                });

            migrationBuilder.CreateTable(
                name: "PermissionTerritory",
                schema: "sources",
                columns: table => new
                {
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTerritory", x => new { x.PermissionId, x.Value });
                });

            migrationBuilder.CreateTable(
                name: "ProvenanceEdge",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssetReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Relation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvenanceEdge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvenanceEdge_Assets_AssetReferenceId",
                        column: x => x.AssetReferenceId,
                        principalSchema: "core",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvenanceEdge_Revisions_ContentRevisionId",
                        column: x => x.ContentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvenanceEdge_Revisions_ParentRevisionId",
                        column: x => x.ParentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemediationTask",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceUseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResolutionEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationTask", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemediationTask_SourceUse_SourceUseId",
                        column: x => x.SourceUseId,
                        principalSchema: "sources",
                        principalTable: "SourceUse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RightsDecision",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Territory = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Commercial = table.Column<bool>(type: "bit", nullable: false),
                    Adaptation = table.Column<bool>(type: "bit", nullable: false),
                    Translation = table.Column<bool>(type: "bit", nullable: false),
                    AiProcessing = table.Column<bool>(type: "bit", nullable: false),
                    Allowed = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RightsDecision", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceEdition",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TranslatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublisherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locator = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CitationFormat = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceEdition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceEdition_Locales_Language",
                        column: x => x.Language,
                        principalSchema: "core",
                        principalTable: "Locales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceEdition_SourceParty_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "sources",
                        principalTable: "SourceParty",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceEdition_SourceParty_PublisherId",
                        column: x => x.PublisherId,
                        principalSchema: "sources",
                        principalTable: "SourceParty",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceEdition_SourceParty_TranslatorId",
                        column: x => x.TranslatorId,
                        principalSchema: "sources",
                        principalTable: "SourceParty",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceEdition_SourceWork_WorkId",
                        column: x => x.WorkId,
                        principalSchema: "sources",
                        principalTable: "SourceWork",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourcePermission",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Commercial = table.Column<bool>(type: "bit", nullable: false),
                    Adaptation = table.Column<bool>(type: "bit", nullable: false),
                    Translation = table.Column<bool>(type: "bit", nullable: false),
                    AiProcessing = table.Column<bool>(type: "bit", nullable: false),
                    SnapshotRetention = table.Column<bool>(type: "bit", nullable: false),
                    RequiredAttribution = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourcePermission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourcePermission_SourceEdition_EditionId",
                        column: x => x.EditionId,
                        principalSchema: "sources",
                        principalTable: "SourceEdition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceRevocation",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceRevocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceRevocation_SourceEdition_EditionId",
                        column: x => x.EditionId,
                        principalSchema: "sources",
                        principalTable: "SourceEdition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceRevocation_SourceWork_WorkId",
                        column: x => x.WorkId,
                        principalSchema: "sources",
                        principalTable: "SourceWork",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceSnapshot",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportPermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RetrievedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceSnapshot_SourceEdition_EditionId",
                        column: x => x.EditionId,
                        principalSchema: "sources",
                        principalTable: "SourceEdition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceSnapshot_SourcePermission_ImportPermissionId",
                        column: x => x.ImportPermissionId,
                        principalSchema: "sources",
                        principalTable: "SourcePermission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProvenanceEdge_AssetReferenceId",
                schema: "sources",
                table: "ProvenanceEdge",
                column: "AssetReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvenanceEdge_ContentRevisionId",
                schema: "sources",
                table: "ProvenanceEdge",
                column: "ContentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvenanceEdge_ParentRevisionId",
                schema: "sources",
                table: "ProvenanceEdge",
                column: "ParentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvenanceEdge_SnapshotId",
                schema: "sources",
                table: "ProvenanceEdge",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTask_RevocationId_SourceUseId",
                schema: "sources",
                table: "RemediationTask",
                columns: new[] { "RevocationId", "SourceUseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTask_SourceUseId",
                schema: "sources",
                table: "RemediationTask",
                column: "SourceUseId");

            migrationBuilder.CreateIndex(
                name: "IX_RightsDecision_PermissionId",
                schema: "sources",
                table: "RightsDecision",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RightsDecision_SnapshotId",
                schema: "sources",
                table: "RightsDecision",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceCommand_TenantId_OperationId",
                schema: "sources",
                table: "SourceCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceEdition_AuthorId",
                schema: "sources",
                table: "SourceEdition",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceEdition_Language",
                schema: "sources",
                table: "SourceEdition",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_SourceEdition_PermissionId",
                schema: "sources",
                table: "SourceEdition",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceEdition_PublisherId",
                schema: "sources",
                table: "SourceEdition",
                column: "PublisherId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceEdition_TranslatorId",
                schema: "sources",
                table: "SourceEdition",
                column: "TranslatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceEdition_WorkId_Key",
                schema: "sources",
                table: "SourceEdition",
                columns: new[] { "WorkId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceEdition_WorkId_Version",
                schema: "sources",
                table: "SourceEdition",
                columns: new[] { "WorkId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceParty_TenantId_Name",
                schema: "sources",
                table: "SourceParty",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourcePermission_EditionId_Version",
                schema: "sources",
                table: "SourcePermission",
                columns: new[] { "EditionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceRevocation_EditionId",
                schema: "sources",
                table: "SourceRevocation",
                column: "EditionId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceRevocation_WorkId",
                schema: "sources",
                table: "SourceRevocation",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceSnapshot_EditionId_Sha256",
                schema: "sources",
                table: "SourceSnapshot",
                columns: new[] { "EditionId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceSnapshot_ImportPermissionId",
                schema: "sources",
                table: "SourceSnapshot",
                column: "ImportPermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceUse_ContentRevisionId",
                schema: "sources",
                table: "SourceUse",
                column: "ContentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceUse_TenantId_OperationId",
                schema: "sources",
                table: "SourceUse",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceWork_TenantId_UniverseId_Key",
                schema: "sources",
                table: "SourceWork",
                columns: new[] { "TenantId", "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceWork_UniverseId_TenantId",
                schema: "sources",
                table: "SourceWork",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PermissionFormat_SourcePermission_PermissionId",
                schema: "sources",
                table: "PermissionFormat",
                column: "PermissionId",
                principalSchema: "sources",
                principalTable: "SourcePermission",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PermissionLanguage_SourcePermission_PermissionId",
                schema: "sources",
                table: "PermissionLanguage",
                column: "PermissionId",
                principalSchema: "sources",
                principalTable: "SourcePermission",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PermissionTerritory_SourcePermission_PermissionId",
                schema: "sources",
                table: "PermissionTerritory",
                column: "PermissionId",
                principalSchema: "sources",
                principalTable: "SourcePermission",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProvenanceEdge_SourceSnapshot_SnapshotId",
                schema: "sources",
                table: "ProvenanceEdge",
                column: "SnapshotId",
                principalSchema: "sources",
                principalTable: "SourceSnapshot",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RemediationTask_SourceRevocation_RevocationId",
                schema: "sources",
                table: "RemediationTask",
                column: "RevocationId",
                principalSchema: "sources",
                principalTable: "SourceRevocation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RightsDecision_SourcePermission_PermissionId",
                schema: "sources",
                table: "RightsDecision",
                column: "PermissionId",
                principalSchema: "sources",
                principalTable: "SourcePermission",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RightsDecision_SourceSnapshot_SnapshotId",
                schema: "sources",
                table: "RightsDecision",
                column: "SnapshotId",
                principalSchema: "sources",
                principalTable: "SourceSnapshot",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SourceEdition_SourcePermission_PermissionId",
                schema: "sources",
                table: "SourceEdition",
                column: "PermissionId",
                principalSchema: "sources",
                principalTable: "SourcePermission",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SourceEdition_SourcePermission_PermissionId",
                schema: "sources",
                table: "SourceEdition");

            migrationBuilder.DropTable(
                name: "PermissionFormat",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "PermissionLanguage",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "PermissionTerritory",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "ProvenanceEdge",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "RemediationTask",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "RightsDecision",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceAudit",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceCommand",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceRevocation",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceUse",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceSnapshot",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourcePermission",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceEdition",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceParty",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "SourceWork",
                schema: "sources");
        }
    }
}
