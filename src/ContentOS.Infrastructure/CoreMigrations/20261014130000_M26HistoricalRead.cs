using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M26HistoricalRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CorpusVerseId",
                schema: "reader",
                table: "ReadPiece",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "HistoricalJson",
                schema: "reader",
                table: "ReadPiece",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ManifestId",
                schema: "reader",
                table: "ReadCollection",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "ExpectedPieces",
                schema: "reader",
                table: "ReadCollection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialKind",
                schema: "corpus",
                table: "CorpusManifest",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Scripture");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS(SELECT 1 FROM [reader].[ReadCollection] WHERE [ManifestId] IS NULL) OR EXISTS(SELECT 1 FROM [corpus].[CorpusManifest] WHERE [MaterialKind] = N'History') THROW 51000, 'Historical data exists; use compatible restore or forward fix, never destroy acknowledged history.', 1;");
            migrationBuilder.DropColumn(
                name: "HistoricalJson",
                schema: "reader",
                table: "ReadPiece");

            migrationBuilder.DropColumn(
                name: "ExpectedPieces",
                schema: "reader",
                table: "ReadCollection");

            migrationBuilder.DropColumn(
                name: "MaterialKind",
                schema: "corpus",
                table: "CorpusManifest");

            migrationBuilder.AlterColumn<Guid>(
                name: "CorpusVerseId",
                schema: "reader",
                table: "ReadPiece",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ManifestId",
                schema: "reader",
                table: "ReadCollection",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
