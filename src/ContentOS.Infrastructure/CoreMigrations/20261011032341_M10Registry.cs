using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M10Registry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "registry");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CharacterRevisions_Id_CharacterId",
                schema: "core",
                table: "CharacterRevisions",
                columns: new[] { "Id", "CharacterId" });

            migrationBuilder.CreateTable(
                name: "RegistryEpisode",
                schema: "registry",
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
                    table.PrimaryKey("PK_RegistryEpisode", x => x.Id);
                    table.UniqueConstraint("AK_RegistryEpisode_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_RegistryEpisode_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistryOperation",
                schema: "registry",
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
                    table.PrimaryKey("PK_RegistryOperation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistryOperation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistryRevision",
                schema: "registry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CoreRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OtherCharacterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EpisodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EscalationEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistryRevision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistryRevision_CharacterRevisions_CoreRevisionId_CharacterId",
                        columns: x => new { x.CoreRevisionId, x.CharacterId },
                        principalSchema: "core",
                        principalTable: "CharacterRevisions",
                        principalColumns: new[] { "Id", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistryRevision_Characters_CharacterId_UniverseId",
                        columns: x => new { x.CharacterId, x.UniverseId },
                        principalSchema: "core",
                        principalTable: "Characters",
                        principalColumns: new[] { "Id", "UniverseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistryRevision_Characters_OtherCharacterId_UniverseId",
                        columns: x => new { x.OtherCharacterId, x.UniverseId },
                        principalSchema: "core",
                        principalTable: "Characters",
                        principalColumns: new[] { "Id", "UniverseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistryRevision_RegistryEpisode_EpisodeId_UniverseId_TenantId",
                        columns: x => new { x.EpisodeId, x.UniverseId, x.TenantId },
                        principalSchema: "registry",
                        principalTable: "RegistryEpisode",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistryRevision_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistryEpisode_TenantId_UniverseId_Key",
                schema: "registry",
                table: "RegistryEpisode",
                columns: new[] { "TenantId", "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistryEpisode_UniverseId_TenantId",
                schema: "registry",
                table: "RegistryEpisode",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistryOperation_TenantId_OperationId",
                schema: "registry",
                table: "RegistryOperation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistryOperation_UniverseId_TenantId",
                schema: "registry",
                table: "RegistryOperation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistryRevision_CharacterId_UniverseId",
                schema: "registry",
                table: "RegistryRevision",
                columns: new[] { "CharacterId", "UniverseId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistryRevision_CoreRevisionId_CharacterId",
                schema: "registry",
                table: "RegistryRevision",
                columns: new[] { "CoreRevisionId", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistryRevision_EpisodeId_UniverseId_TenantId",
                schema: "registry",
                table: "RegistryRevision",
                columns: new[] { "EpisodeId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistryRevision_OtherCharacterId_UniverseId",
                schema: "registry",
                table: "RegistryRevision",
                columns: new[] { "OtherCharacterId", "UniverseId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistryRevision_TenantId_UniverseId_Kind_Key_Version",
                schema: "registry",
                table: "RegistryRevision",
                columns: new[] { "TenantId", "UniverseId", "Kind", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistryRevision_UniverseId_TenantId",
                schema: "registry",
                table: "RegistryRevision",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistryOperation",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "RegistryRevision",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "RegistryEpisode",
                schema: "registry");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CharacterRevisions_Id_CharacterId",
                schema: "core",
                table: "CharacterRevisions");
        }
    }
}
