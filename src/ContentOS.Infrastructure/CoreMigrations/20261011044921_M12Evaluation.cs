using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M12Evaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "evaluation");

            migrationBuilder.CreateTable(
                name: "EvaluationCandidate",
                schema: "evaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCandidate", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationCandidate_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EvaluationCandidate_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationDataset",
                schema: "evaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationDataset", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationDataset_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EvaluationDataset_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationOperation",
                schema: "evaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationOperation", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationOperation_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EvaluationOperation_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationRun",
                schema: "evaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaselineRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Split = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRun", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationRun_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EvaluationRun_EvaluationCandidate_CandidateId_UniverseId_TenantId",
                        columns: x => new { x.CandidateId, x.UniverseId, x.TenantId },
                        principalSchema: "evaluation",
                        principalTable: "EvaluationCandidate",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRun_EvaluationDataset_DatasetId_UniverseId_TenantId",
                        columns: x => new { x.DatasetId, x.UniverseId, x.TenantId },
                        principalSchema: "evaluation",
                        principalTable: "EvaluationDataset",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRun_EvaluationRun_BaselineRunId_UniverseId_TenantId",
                        columns: x => new { x.BaselineRunId, x.UniverseId, x.TenantId },
                        principalSchema: "evaluation",
                        principalTable: "EvaluationRun",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRun_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationAdvice",
                schema: "evaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationAdvice", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationAdvice_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EvaluationAdvice_EvaluationRun_RunId_UniverseId_TenantId",
                        columns: x => new { x.RunId, x.UniverseId, x.TenantId },
                        principalSchema: "evaluation",
                        principalTable: "EvaluationRun",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationAdvice_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationReview",
                schema: "evaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationReview", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationReview_Id_UniverseId_TenantId", x => new { x.Id, x.UniverseId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_EvaluationReview_EvaluationDataset_DatasetId_UniverseId_TenantId",
                        columns: x => new { x.DatasetId, x.UniverseId, x.TenantId },
                        principalSchema: "evaluation",
                        principalTable: "EvaluationDataset",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationReview_EvaluationRun_RunId_UniverseId_TenantId",
                        columns: x => new { x.RunId, x.UniverseId, x.TenantId },
                        principalSchema: "evaluation",
                        principalTable: "EvaluationRun",
                        principalColumns: new[] { "Id", "UniverseId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationReview_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationAdvice_RunId_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationAdvice",
                columns: new[] { "RunId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationAdvice_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationAdvice",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCandidate_TenantId_UniverseId_Key_Version",
                schema: "evaluation",
                table: "EvaluationCandidate",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCandidate_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationCandidate",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationDataset_TenantId_UniverseId_Key_Version",
                schema: "evaluation",
                table: "EvaluationDataset",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationDataset_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationDataset",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationOperation_TenantId_OperationId",
                schema: "evaluation",
                table: "EvaluationOperation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationOperation_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationOperation",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReview_DatasetId_CaseKey_RunId_Mode",
                schema: "evaluation",
                table: "EvaluationReview",
                columns: new[] { "DatasetId", "CaseKey", "RunId", "Mode" },
                unique: true,
                filter: "[RunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReview_DatasetId_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationReview",
                columns: new[] { "DatasetId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReview_RunId_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationReview",
                columns: new[] { "RunId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReview_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationReview",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRun_BaselineRunId_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationRun",
                columns: new[] { "BaselineRunId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRun_CandidateId_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationRun",
                columns: new[] { "CandidateId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRun_DatasetId_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationRun",
                columns: new[] { "DatasetId", "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRun_UniverseId_TenantId",
                schema: "evaluation",
                table: "EvaluationRun",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationAdvice",
                schema: "evaluation");

            migrationBuilder.DropTable(
                name: "EvaluationOperation",
                schema: "evaluation");

            migrationBuilder.DropTable(
                name: "EvaluationReview",
                schema: "evaluation");

            migrationBuilder.DropTable(
                name: "EvaluationRun",
                schema: "evaluation");

            migrationBuilder.DropTable(
                name: "EvaluationCandidate",
                schema: "evaluation");

            migrationBuilder.DropTable(
                name: "EvaluationDataset",
                schema: "evaluation");
        }
    }
}
