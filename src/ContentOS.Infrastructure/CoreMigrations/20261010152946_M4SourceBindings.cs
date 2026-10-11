using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M4SourceBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorpusSourceBinding",
                schema: "corpus",
                columns: table => new
                {
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<int>(type: "int", nullable: false),
                    Verse = table.Column<int>(type: "int", nullable: false),
                    VariantKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceChapter = table.Column<int>(type: "int", nullable: false),
                    SourceVerse = table.Column<int>(type: "int", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceLocator = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusSourceBinding", x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey, x.Kind, x.Locale });
                    table.ForeignKey(
                        name: "FK_CorpusSourceBinding_CorpusLocator_ManifestId_Chapter_Verse_VariantKey",
                        columns: x => new { x.ManifestId, x.Chapter, x.Verse, x.VariantKey },
                        principalSchema: "corpus",
                        principalTable: "CorpusLocator",
                        principalColumns: new[] { "ManifestId", "Chapter", "Verse", "VariantKey" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorpusSourceBinding_SourceSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "sources",
                        principalTable: "SourceSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorpusSourceBinding_SnapshotId",
                schema: "corpus",
                table: "CorpusSourceBinding",
                column: "SnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorpusSourceBinding",
                schema: "corpus");
        }
    }
}
