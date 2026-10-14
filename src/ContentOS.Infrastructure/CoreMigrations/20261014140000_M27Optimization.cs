using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M27Optimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "optimization");

            migrationBuilder.CreateTable(
                name: "OptimizationAutomationRun",
                schema: "optimization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationAutomationRun", x => x.Id);
                    table.UniqueConstraint("AK_OptimizationAutomationRun_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_OptimizationAutomationRun_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OptimizationExperiment",
                schema: "optimization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProposalJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Dispatches = table.Column<int>(type: "int", nullable: false),
                    ReservedCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationExperiment", x => x.Id);
                    table.UniqueConstraint("AK_OptimizationExperiment_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_OptimizationExperiment_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OptimizationEvent",
                schema: "optimization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationEvent", x => x.Id);
                    table.UniqueConstraint("AK_OptimizationEvent_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_OptimizationEvent_OptimizationExperiment_ExperimentId_UniverseId_TenantId",
                        columns: x => new { x.ExperimentId, x.UniverseId, x.TenantId },
                        principalSchema: "optimization",
                        principalTable: "OptimizationExperiment",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OptimizationEvent_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationAutomationRun_TenantId_OperationId",
                schema: "optimization",
                table: "OptimizationAutomationRun",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationAutomationRun_UniverseId_TenantId",
                schema: "optimization",
                table: "OptimizationAutomationRun",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationEvent_ExperimentId_UniverseId_TenantId",
                schema: "optimization",
                table: "OptimizationEvent",
                columns: new[] { "ExperimentId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationEvent_TenantId_OperationId",
                schema: "optimization",
                table: "OptimizationEvent",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationEvent_UniverseId_TenantId",
                schema: "optimization",
                table: "OptimizationEvent",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationExperiment_TenantId_UniverseId_Key_Version",
                schema: "optimization",
                table: "OptimizationExperiment",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationExperiment_UniverseId_TenantId",
                schema: "optimization",
                table: "OptimizationExperiment",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS(SELECT 1 FROM [optimization].[OptimizationExperiment]) OR EXISTS(SELECT 1 FROM [optimization].[OptimizationAutomationRun]) THROW 51000, 'Optimization evidence exists; use compatible restore or a forward fix.', 1;");
            migrationBuilder.DropTable(
                name: "OptimizationAutomationRun",
                schema: "optimization");

            migrationBuilder.DropTable(
                name: "OptimizationEvent",
                schema: "optimization");

            migrationBuilder.DropTable(
                name: "OptimizationExperiment",
                schema: "optimization");
        }
    }
}
