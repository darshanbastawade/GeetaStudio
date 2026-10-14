using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M19Analytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.CreateTable(
                name: "AnalyticsCommand",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsCommand", x => x.Id);
                    table.UniqueConstraint("AK_AnalyticsCommand_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AnalyticsCommand_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsConsent",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Fixture = table.Column<bool>(type: "bit", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsConsent", x => x.Id);
                    table.UniqueConstraint("AK_AnalyticsConsent_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AnalyticsConsent_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsDefinition",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsDefinition", x => x.Id);
                    table.UniqueConstraint("AK_AnalyticsDefinition_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AnalyticsDefinition_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsForecast",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsForecast", x => x.Id);
                    table.UniqueConstraint("AK_AnalyticsForecast_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AnalyticsForecast_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsFx",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsFx", x => x.Id);
                    table.UniqueConstraint("AK_AnalyticsFx_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AnalyticsFx_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsSource",
                schema: "analytics",
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
                    table.PrimaryKey("PK_AnalyticsSource", x => x.Id);
                    table.UniqueConstraint("AK_AnalyticsSource_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AnalyticsSource_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsEntry",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EconomicKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    ReportingAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    ReportingCurrency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsEntry", x => x.Id);
                    table.UniqueConstraint("AK_AnalyticsEntry_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_AnalyticsConsent_ConsentId_UniverseId_TenantId",
                        columns: x => new { x.ConsentId, x.UniverseId, x.TenantId },
                        principalSchema: "analytics",
                        principalTable: "AnalyticsConsent",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_AnalyticsDefinition_DefinitionId_UniverseId_TenantId",
                        columns: x => new { x.DefinitionId, x.UniverseId, x.TenantId },
                        principalSchema: "analytics",
                        principalTable: "AnalyticsDefinition",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_AnalyticsEntry_RelatedEntryId_UniverseId_TenantId",
                        columns: x => new { x.RelatedEntryId, x.UniverseId, x.TenantId },
                        principalSchema: "analytics",
                        principalTable: "AnalyticsEntry",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_AnalyticsFx_FxId_UniverseId_TenantId",
                        columns: x => new { x.FxId, x.UniverseId, x.TenantId },
                        principalSchema: "analytics",
                        principalTable: "AnalyticsFx",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_AnalyticsSource_SourceId_UniverseId_TenantId",
                        columns: x => new { x.SourceId, x.UniverseId, x.TenantId },
                        principalSchema: "analytics",
                        principalTable: "AnalyticsSource",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_SocialPublication_PublicationId_UniverseId_TenantId",
                        columns: x => new { x.PublicationId, x.UniverseId, x.TenantId },
                        principalSchema: "social",
                        principalTable: "SocialPublication",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEntry_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsCommand_TenantId_OperationId",
                schema: "analytics",
                table: "AnalyticsCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsCommand_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsCommand",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsConsent_TenantId_UniverseId_SubjectReference_Purpose",
                schema: "analytics",
                table: "AnalyticsConsent",
                columns: new[] { "TenantId", "UniverseId", "SubjectReference", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsConsent_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsConsent",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDefinition_TenantId_UniverseId_Key_Version",
                schema: "analytics",
                table: "AnalyticsDefinition",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDefinition_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsDefinition",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_ConsentId_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "ConsentId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_DefinitionId_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "DefinitionId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_FxId_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "FxId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_PublicationId_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "PublicationId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_RelatedEntryId_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "RelatedEntryId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_RevisionId",
                schema: "analytics",
                table: "AnalyticsEntry",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_SourceId_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "SourceId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_TenantId_UniverseId_Kind_EconomicKey",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "TenantId", "UniverseId", "Kind", "EconomicKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_TenantId_UniverseId_OccurredUtc_CreatedUtc",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "TenantId", "UniverseId", "OccurredUtc", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_TenantId_UniverseId_SourceSystem_AccountReference_EventId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "TenantId", "UniverseId", "SourceSystem", "AccountReference", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEntry_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsEntry",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsForecast_TenantId_UniverseId_Key_Version",
                schema: "analytics",
                table: "AnalyticsForecast",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsForecast_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsForecast",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsFx_TenantId_UniverseId_Key_Version",
                schema: "analytics",
                table: "AnalyticsFx",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsFx_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsFx",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSource_TenantId_UniverseId_Key_Version",
                schema: "analytics",
                table: "AnalyticsSource",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSource_UniverseId_TenantId",
                schema: "analytics",
                table: "AnalyticsSource",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsCommand",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "AnalyticsEntry",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "AnalyticsForecast",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "AnalyticsConsent",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "AnalyticsDefinition",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "AnalyticsFx",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "AnalyticsSource",
                schema: "analytics");
        }
    }
}
