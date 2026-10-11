using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M9Lessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lessons");

            migrationBuilder.CreateTable(
                name: "LessonBundle",
                schema: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnchorClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterpretationClaimRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonBundle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonBundle_ClaimRevision_AnchorClaimRevisionId",
                        column: x => x.AnchorClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonBundle_ClaimRevision_InterpretationClaimRevisionId",
                        column: x => x.InterpretationClaimRevisionId,
                        principalSchema: "evidence",
                        principalTable: "ClaimRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonBundle_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonCommand",
                schema: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_LessonCommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonCommand_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonProfile",
                schema: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonProfile_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonVariant",
                schema: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BundleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    SectionsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonVariant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonVariant_LessonBundle_BundleId",
                        column: x => x.BundleId,
                        principalSchema: "lessons",
                        principalTable: "LessonBundle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonVariant_LessonVariant_ParentVariantId",
                        column: x => x.ParentVariantId,
                        principalSchema: "lessons",
                        principalTable: "LessonVariant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonVariant_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonMemory",
                schema: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocaleProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BundleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonMemory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonMemory_LessonBundle_BundleId",
                        column: x => x.BundleId,
                        principalSchema: "lessons",
                        principalTable: "LessonBundle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonMemory_LessonProfile_LocaleProfileId",
                        column: x => x.LocaleProfileId,
                        principalSchema: "lessons",
                        principalTable: "LessonProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonMemory_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonBundle_AnchorClaimRevisionId",
                schema: "lessons",
                table: "LessonBundle",
                column: "AnchorClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonBundle_InterpretationClaimRevisionId",
                schema: "lessons",
                table: "LessonBundle",
                column: "InterpretationClaimRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonBundle_UniverseId_TenantId",
                schema: "lessons",
                table: "LessonBundle",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonCommand_TenantId_OperationId",
                schema: "lessons",
                table: "LessonCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonCommand_UniverseId_TenantId",
                schema: "lessons",
                table: "LessonCommand",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonMemory_BundleId",
                schema: "lessons",
                table: "LessonMemory",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonMemory_LocaleProfileId",
                schema: "lessons",
                table: "LessonMemory",
                column: "LocaleProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonMemory_UniverseId_TenantId",
                schema: "lessons",
                table: "LessonMemory",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonProfile_TenantId_UniverseId_Key_Version",
                schema: "lessons",
                table: "LessonProfile",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonProfile_UniverseId_TenantId",
                schema: "lessons",
                table: "LessonProfile",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonVariant_BundleId",
                schema: "lessons",
                table: "LessonVariant",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVariant_ParentVariantId",
                schema: "lessons",
                table: "LessonVariant",
                column: "ParentVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVariant_UniverseId_TenantId",
                schema: "lessons",
                table: "LessonVariant",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonCommand",
                schema: "lessons");

            migrationBuilder.DropTable(
                name: "LessonMemory",
                schema: "lessons");

            migrationBuilder.DropTable(
                name: "LessonVariant",
                schema: "lessons");

            migrationBuilder.DropTable(
                name: "LessonProfile",
                schema: "lessons");

            migrationBuilder.DropTable(
                name: "LessonBundle",
                schema: "lessons");
        }
    }
}
