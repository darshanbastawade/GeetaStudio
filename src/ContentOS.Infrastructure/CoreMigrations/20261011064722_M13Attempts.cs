using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M13Attempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EditorialAttempt",
                schema: "editorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReplacementContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialAttempt", x => x.Id);
                    table.UniqueConstraint("AK_EditorialAttempt_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EditorialAttempt_Contents_ReplacementContentId_UniverseId_TenantId",
                        columns: x => new { x.ReplacementContentId, x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Contents",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialAttempt_EditorialCorrectionTask_TaskId_UniverseId_TenantId",
                        columns: x => new { x.TaskId, x.UniverseId, x.TenantId },
                        principalSchema: "editorial",
                        principalTable: "EditorialCorrectionTask",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialAttempt_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAttempt_ReplacementContentId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialAttempt",
                columns: new[] { "ReplacementContentId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAttempt_TaskId_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialAttempt",
                columns: new[] { "TaskId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAttempt_UniverseId_TenantId",
                schema: "editorial",
                table: "EditorialAttempt",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditorialAttempt",
                schema: "editorial");
        }
    }
}
