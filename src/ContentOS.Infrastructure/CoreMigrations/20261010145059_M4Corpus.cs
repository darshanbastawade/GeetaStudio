using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M4Corpus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "corpus");

            migrationBuilder.CreateTable(
                name: "CorpusCommand",
                schema: "corpus",
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
                    table.PrimaryKey("PK_CorpusCommand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorpusManifest",
                schema: "corpus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BaselineTarget = table.Column<int>(type: "int", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusManifest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorpusManifest_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorpusLocator",
                schema: "corpus",
                columns: table => new
                {
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<int>(type: "int", nullable: false),
                    Verse = table.Column<int>(type: "int", nullable: false),
                    VariantKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaselineVerse = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusLocator", x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey });
                    table.ForeignKey(
                        name: "FK_CorpusLocator_CorpusManifest_ManifestId",
                        column: x => x.ManifestId,
                        principalSchema: "corpus",
                        principalTable: "CorpusManifest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorpusVerse",
                schema: "corpus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<int>(type: "int", nullable: false),
                    Verse = table.Column<int>(type: "int", nullable: false),
                    VariantKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusVerse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorpusVerse_CorpusLocator_ManifestId_Chapter_Verse_VariantKey",
                        columns: x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey },
                        principalSchema: "corpus",
                        principalTable: "CorpusLocator",
                        principalColumns: new[] { "ManifestId", "Chapter", "Verse", "VariantKey" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorpusAnnotation",
                schema: "corpus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusAnnotation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorpusAnnotation_CorpusVerse_VerseId",
                        column: x => x.VerseId,
                        principalSchema: "corpus",
                        principalTable: "CorpusVerse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorpusAnnotation_Locales_Locale",
                        column: x => x.Locale,
                        principalSchema: "core",
                        principalTable: "Locales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorpusAnnotation_SourceSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "sources",
                        principalTable: "SourceSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorpusText",
                schema: "corpus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExactText = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLocator = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusText", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorpusText_CorpusVerse_VerseId",
                        column: x => x.VerseId,
                        principalSchema: "corpus",
                        principalTable: "CorpusVerse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorpusText_Locales_Locale",
                        column: x => x.Locale,
                        principalSchema: "core",
                        principalTable: "Locales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorpusText_SourceSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "sources",
                        principalTable: "SourceSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorpusAnnotation_Locale",
                schema: "corpus",
                table: "CorpusAnnotation",
                column: "Locale");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusAnnotation_SnapshotId",
                schema: "corpus",
                table: "CorpusAnnotation",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusAnnotation_VerseId_Key",
                schema: "corpus",
                table: "CorpusAnnotation",
                columns: new[] { "VerseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorpusCommand_TenantId_OperationId",
                schema: "corpus",
                table: "CorpusCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorpusManifest_UniverseId_Key_Version",
                schema: "corpus",
                table: "CorpusManifest",
                columns: new[] { "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorpusManifest_UniverseId_TenantId",
                schema: "corpus",
                table: "CorpusManifest",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CorpusText_Locale",
                schema: "corpus",
                table: "CorpusText",
                column: "Locale");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusText_SnapshotId",
                schema: "corpus",
                table: "CorpusText",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusText_VerseId_Kind_Locale",
                schema: "corpus",
                table: "CorpusText",
                columns: new[] { "VerseId", "Kind", "Locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorpusVerse_ManifestId_Chapter_Verse_VariantKey",
                schema: "corpus",
                table: "CorpusVerse",
                columns: new[] { "ManifestId", "Chapter", "Verse", "VariantKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorpusAnnotation",
                schema: "corpus");

            migrationBuilder.DropTable(
                name: "CorpusCommand",
                schema: "corpus");

            migrationBuilder.DropTable(
                name: "CorpusText",
                schema: "corpus");

            migrationBuilder.DropTable(
                name: "CorpusVerse",
                schema: "corpus");

            migrationBuilder.DropTable(
                name: "CorpusLocator",
                schema: "corpus");

            migrationBuilder.DropTable(
                name: "CorpusManifest",
                schema: "corpus");
        }
    }
}
