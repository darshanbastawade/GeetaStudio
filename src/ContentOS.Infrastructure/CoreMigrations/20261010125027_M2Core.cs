using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M2Core : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "Locales",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => new { x.TenantId, x.Actor });
                    table.ForeignKey(
                        name: "FK_Members_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Universes",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublicKnowledgeRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Universes", x => x.Id);
                    table.UniqueConstraint("AK_Universes_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_Universes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Audiences",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audiences", x => x.Id);
                    table.UniqueConstraint("AK_Audiences_Id_UniverseId", x => new { x.Id, x.UniverseId });
                    table.ForeignKey(
                        name: "FK_Audiences_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PersistenceScope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.UniqueConstraint("AK_Characters_Id_UniverseId", x => new { x.Id, x.UniverseId });
                    table.ForeignKey(
                        name: "FK_Characters_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Grants",
                schema: "core",
                columns: table => new
                {
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GranteeTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Read = table.Column<bool>(type: "bit", nullable: false),
                    Write = table.Column<bool>(type: "bit", nullable: false),
                    Export = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grants", x => new { x.UniverseId, x.GranteeTenantId });
                    table.ForeignKey(
                        name: "FK_Grants_Tenants_GranteeTenantId",
                        column: x => x.GranteeTenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Grants_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Packs",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Packs_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.UniqueConstraint("AK_Subjects_Id_UniverseId", x => new { x.Id, x.UniverseId });
                    table.ForeignKey(
                        name: "FK_Subjects_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Aliases",
                schema: "core",
                columns: table => new
                {
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CharacterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aliases", x => new { x.UniverseId, x.Name });
                    table.ForeignKey(
                        name: "FK_Aliases_Characters_CharacterId_UniverseId",
                        columns: x => new { x.CharacterId, x.UniverseId },
                        principalSchema: "core",
                        principalTable: "Characters",
                        principalColumns: new[] { "Id", "UniverseId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterRevisions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LifeStage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Biography = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FictionalStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterRevisions_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "core",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Relationships",
                schema: "core",
                columns: table => new
                {
                    FromCharacterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToCharacterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relationships", x => new { x.FromCharacterId, x.ToCharacterId, x.Kind });
                    table.ForeignKey(
                        name: "FK_Relationships_Characters_FromCharacterId",
                        column: x => x.FromCharacterId,
                        principalSchema: "core",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Relationships_Characters_ToCharacterId",
                        column: x => x.ToCharacterId,
                        principalSchema: "core",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackAudiences",
                schema: "core",
                columns: table => new
                {
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AudienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackAudiences", x => new { x.PackId, x.AudienceId });
                    table.ForeignKey(
                        name: "FK_PackAudiences_Audiences_AudienceId",
                        column: x => x.AudienceId,
                        principalSchema: "core",
                        principalTable: "Audiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackAudiences_Packs_PackId",
                        column: x => x.PackId,
                        principalSchema: "core",
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackCharacters",
                schema: "core",
                columns: table => new
                {
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackCharacters", x => new { x.PackId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_PackCharacters_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "core",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackCharacters_Packs_PackId",
                        column: x => x.PackId,
                        principalSchema: "core",
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                schema: "core",
                columns: table => new
                {
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rule = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => new { x.PackId, x.Key });
                    table.ForeignKey(
                        name: "FK_Policies_Packs_PackId",
                        column: x => x.PackId,
                        principalSchema: "core",
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceRules",
                schema: "core",
                columns: table => new
                {
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rule = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceRules", x => new { x.PackId, x.Kind });
                    table.ForeignKey(
                        name: "FK_SourceRules_Packs_PackId",
                        column: x => x.PackId,
                        principalSchema: "core",
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubjectTypes",
                schema: "core",
                columns: table => new
                {
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectTypes", x => new { x.PackId, x.Key });
                    table.ForeignKey(
                        name: "FK_SubjectTypes_Packs_PackId",
                        column: x => x.PackId,
                        principalSchema: "core",
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                schema: "core",
                columns: table => new
                {
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => new { x.PackId, x.Key });
                    table.ForeignKey(
                        name: "FK_Terms_Packs_PackId",
                        column: x => x.PackId,
                        principalSchema: "core",
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contents",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AudienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocaleId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    EditorialState = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublicKnowledge = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contents_Audiences_AudienceId_UniverseId",
                        columns: x => new { x.AudienceId, x.UniverseId },
                        principalSchema: "core",
                        principalTable: "Audiences",
                        principalColumns: new[] { "Id", "UniverseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contents_Locales_LocaleId",
                        column: x => x.LocaleId,
                        principalSchema: "core",
                        principalTable: "Locales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contents_Subjects_SubjectId_UniverseId",
                        columns: x => new { x.SubjectId, x.UniverseId },
                        principalSchema: "core",
                        principalTable: "Subjects",
                        principalColumns: new[] { "Id", "UniverseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contents_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Operations_Contents_ContentId",
                        column: x => x.ContentId,
                        principalSchema: "core",
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Revisions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RightsStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provenance = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Revisions_Contents_ContentId",
                        column: x => x.ContentId,
                        principalSchema: "core",
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RightsStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_Revisions_ContentRevisionId",
                        column: x => x.ContentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_Revisions_ContentRevisionId",
                        column: x => x.ContentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Presentations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CharacterRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocaleId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AudienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    VisualAssetReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoiceAssetReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Approval = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presentations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Presentations_Assets_VisualAssetReferenceId",
                        column: x => x.VisualAssetReferenceId,
                        principalSchema: "core",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presentations_Assets_VoiceAssetReferenceId",
                        column: x => x.VoiceAssetReferenceId,
                        principalSchema: "core",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presentations_Audiences_AudienceId",
                        column: x => x.AudienceId,
                        principalSchema: "core",
                        principalTable: "Audiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presentations_CharacterRevisions_CharacterRevisionId",
                        column: x => x.CharacterRevisionId,
                        principalSchema: "core",
                        principalTable: "CharacterRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presentations_Locales_LocaleId",
                        column: x => x.LocaleId,
                        principalSchema: "core",
                        principalTable: "Locales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cast",
                schema: "core",
                columns: table => new
                {
                    ContentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CharacterRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PresentationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cast", x => new { x.ContentRevisionId, x.CharacterRevisionId });
                    table.ForeignKey(
                        name: "FK_Cast_CharacterRevisions_CharacterRevisionId",
                        column: x => x.CharacterRevisionId,
                        principalSchema: "core",
                        principalTable: "CharacterRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cast_Presentations_PresentationProfileId",
                        column: x => x.PresentationProfileId,
                        principalSchema: "core",
                        principalTable: "Presentations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cast_Revisions_ContentRevisionId",
                        column: x => x.ContentRevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_CharacterId_UniverseId",
                schema: "core",
                table: "Aliases",
                columns: new[] { "CharacterId", "UniverseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ContentRevisionId",
                schema: "core",
                table: "Assets",
                column: "ContentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_UniverseId_Key",
                schema: "core",
                table: "Audiences",
                columns: new[] { "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cast_CharacterRevisionId",
                schema: "core",
                table: "Cast",
                column: "CharacterRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Cast_PresentationProfileId",
                schema: "core",
                table: "Cast",
                column: "PresentationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterRevisions_CharacterId_Version",
                schema: "core",
                table: "CharacterRevisions",
                columns: new[] { "CharacterId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_UniverseId_Key",
                schema: "core",
                table: "Characters",
                columns: new[] { "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contents_AudienceId_UniverseId",
                schema: "core",
                table: "Contents",
                columns: new[] { "AudienceId", "UniverseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contents_LocaleId",
                schema: "core",
                table: "Contents",
                column: "LocaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Contents_SubjectId_UniverseId",
                schema: "core",
                table: "Contents",
                columns: new[] { "SubjectId", "UniverseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contents_TenantId_UniverseId",
                schema: "core",
                table: "Contents",
                columns: new[] { "TenantId", "UniverseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contents_UniverseId_TenantId",
                schema: "core",
                table: "Contents",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_RevisionId",
                schema: "core",
                table: "Events",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_GranteeTenantId",
                schema: "core",
                table: "Grants",
                column: "GranteeTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_ContentId",
                schema: "core",
                table: "Operations",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_PackAudiences_AudienceId",
                schema: "core",
                table: "PackAudiences",
                column: "AudienceId");

            migrationBuilder.CreateIndex(
                name: "IX_PackCharacters_CharacterId",
                schema: "core",
                table: "PackCharacters",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Packs_UniverseId_Version",
                schema: "core",
                table: "Packs",
                columns: new[] { "UniverseId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Presentations_AudienceId",
                schema: "core",
                table: "Presentations",
                column: "AudienceId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentations_CharacterRevisionId",
                schema: "core",
                table: "Presentations",
                column: "CharacterRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentations_LocaleId",
                schema: "core",
                table: "Presentations",
                column: "LocaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentations_VisualAssetReferenceId",
                schema: "core",
                table: "Presentations",
                column: "VisualAssetReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentations_VoiceAssetReferenceId",
                schema: "core",
                table: "Presentations",
                column: "VoiceAssetReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Relationships_ToCharacterId",
                schema: "core",
                table: "Relationships",
                column: "ToCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_ContentId_OperationId",
                schema: "core",
                table: "Revisions",
                columns: new[] { "ContentId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_ContentId_Version",
                schema: "core",
                table: "Revisions",
                columns: new[] { "ContentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_UniverseId_Key",
                schema: "core",
                table: "Subjects",
                columns: new[] { "UniverseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Key",
                schema: "core",
                table: "Tenants",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId",
                schema: "core",
                table: "Tenants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Universes_TenantId_Key",
                schema: "core",
                table: "Universes",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_ContentRevisionId",
                schema: "core",
                table: "Workflows",
                column: "ContentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_TenantId_OperationId",
                schema: "core",
                table: "Workflows",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aliases",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Cast",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Grants",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Members",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Operations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "PackAudiences",
                schema: "core");

            migrationBuilder.DropTable(
                name: "PackCharacters",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Policies",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Relationships",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SourceRules",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SubjectTypes",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Terms",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Workflows",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Presentations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Packs",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Assets",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CharacterRevisions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Revisions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Characters",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Contents",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Audiences",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Locales",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Subjects",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Universes",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "core");
        }
    }
}
