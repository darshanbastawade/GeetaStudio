using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M14Read : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reader");

            migrationBuilder.CreateTable(
                name: "ReadCollection",
                schema: "reader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadCollection", x => x.Id);
                    table.UniqueConstraint("AK_ReadCollection_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_ReadCollection_CorpusManifest_ManifestId",
                        column: x => x.ManifestId,
                        principalSchema: "corpus",
                        principalTable: "CorpusManifest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadCollection_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadOperation",
                schema: "reader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadOperation", x => x.Id);
                    table.UniqueConstraint("AK_ReadOperation_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_ReadOperation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadEvidence",
                schema: "reader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadEvidence", x => x.Id);
                    table.UniqueConstraint("AK_ReadEvidence_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_ReadEvidence_ReadCollection_CollectionId_UniverseId_TenantId",
                        columns: x => new { x.CollectionId, x.UniverseId, x.TenantId },
                        principalSchema: "reader",
                        principalTable: "ReadCollection",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadEvidence_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadPiece",
                schema: "reader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorpusVerseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditorialItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Journey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadPiece", x => x.Id);
                    table.UniqueConstraint("AK_ReadPiece_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_ReadPiece_CorpusVerse_CorpusVerseId",
                        column: x => x.CorpusVerseId,
                        principalSchema: "corpus",
                        principalTable: "CorpusVerse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadPiece_EditorialItem_EditorialItemId_UniverseId_TenantId",
                        columns: x => new { x.EditorialItemId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialItem",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadPiece_LessonVariant_LessonVariantId",
                        column: x => x.LessonVariantId,
                        principalSchema: "lessons",
                        principalTable: "LessonVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadPiece_ReadCollection_CollectionId_UniverseId_TenantId",
                        columns: x => new { x.CollectionId, x.UniverseId, x.TenantId },
                        principalSchema: "reader",
                        principalTable: "ReadCollection",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadPiece_Revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "core",
                        principalTable: "Revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadPiece_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadEventCount",
                schema: "reader",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PieceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadEventCount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadEventCount_ReadCollection_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "reader",
                        principalTable: "ReadCollection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadEventCount_ReadPiece_PieceId",
                        column: x => x.PieceId,
                        principalSchema: "reader",
                        principalTable: "ReadPiece",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadCollection_Key",
                schema: "reader",
                table: "ReadCollection",
                column: "Key",
                unique: true,
                filter: "[State] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_ReadCollection_Key_Version",
                schema: "reader",
                table: "ReadCollection",
                columns: new[] { "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadCollection_ManifestId",
                schema: "reader",
                table: "ReadCollection",
                column: "ManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadCollection_UniverseId_TenantId",
                schema: "reader",
                table: "ReadCollection",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadEventCount_CollectionId_PieceId_Day_Version_Name",
                schema: "reader",
                table: "ReadEventCount",
                columns: new[] { "CollectionId", "PieceId", "Day", "Version", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadEventCount_PieceId",
                schema: "reader",
                table: "ReadEventCount",
                column: "PieceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadEvidence_CollectionId_UniverseId_TenantId",
                schema: "reader",
                table: "ReadEvidence",
                columns: new[] { "CollectionId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadEvidence_UniverseId_TenantId",
                schema: "reader",
                table: "ReadEvidence",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadOperation_TenantId_OperationId",
                schema: "reader",
                table: "ReadOperation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadOperation_UniverseId_TenantId",
                schema: "reader",
                table: "ReadOperation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadPiece_CollectionId_EditorialItemId",
                schema: "reader",
                table: "ReadPiece",
                columns: new[] { "CollectionId", "EditorialItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadPiece_CollectionId_UniverseId_TenantId",
                schema: "reader",
                table: "ReadPiece",
                columns: new[] { "CollectionId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadPiece_CorpusVerseId",
                schema: "reader",
                table: "ReadPiece",
                column: "CorpusVerseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadPiece_EditorialItemId_UniverseId_TenantId",
                schema: "reader",
                table: "ReadPiece",
                columns: new[] { "EditorialItemId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadPiece_LessonVariantId",
                schema: "reader",
                table: "ReadPiece",
                column: "LessonVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadPiece_RevisionId",
                schema: "reader",
                table: "ReadPiece",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadPiece_UniverseId_TenantId",
                schema: "reader",
                table: "ReadPiece",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadEventCount",
                schema: "reader");

            migrationBuilder.DropTable(
                name: "ReadEvidence",
                schema: "reader");

            migrationBuilder.DropTable(
                name: "ReadOperation",
                schema: "reader");

            migrationBuilder.DropTable(
                name: "ReadPiece",
                schema: "reader");

            migrationBuilder.DropTable(
                name: "ReadCollection",
                schema: "reader");
        }
    }
}
