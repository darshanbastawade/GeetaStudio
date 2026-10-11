using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M7AiLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "AiBudget",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capability = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Limit = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    Spent = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiBudget", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiBudget_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiProfile",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiProfile_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiRoute",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Task = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRoute", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRoute_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiRouteHead",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Task = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRouteHead", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRouteHead_AiRoute_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "ai",
                        principalTable: "AiRoute",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiRun",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    Spent = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200, nullable: true),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRun_AiBudget_BudgetId",
                        column: x => x.BudgetId,
                        principalSchema: "ai",
                        principalTable: "AiBudget",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiRun_AiRoute_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "ai",
                        principalTable: "AiRoute",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiRun_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiAttempt",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ceiling = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CachedInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReconciliationEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EndedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAttempt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAttempt_AiProfile_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "ai",
                        principalTable: "AiProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiAttempt_AiRun_RunId",
                        column: x => x.RunId,
                        principalSchema: "ai",
                        principalTable: "AiRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAttempt_ProfileId",
                schema: "ai",
                table: "AiAttempt",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAttempt_RunId_Number",
                schema: "ai",
                table: "AiAttempt",
                columns: new[] { "RunId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiBudget_TenantId_Capability_Currency_StartsUtc",
                schema: "ai",
                table: "AiBudget",
                columns: new[] { "TenantId", "Capability", "Currency", "StartsUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProfile_ConnectionId",
                schema: "ai",
                table: "AiProfile",
                column: "ConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiProfile_TenantId",
                schema: "ai",
                table: "AiProfile",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRoute_TenantId",
                schema: "ai",
                table: "AiRoute",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRouteHead_RouteId",
                schema: "ai",
                table: "AiRouteHead",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRouteHead_TenantId_Task",
                schema: "ai",
                table: "AiRouteHead",
                columns: new[] { "TenantId", "Task" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiRun_BudgetId",
                schema: "ai",
                table: "AiRun",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRun_RouteId",
                schema: "ai",
                table: "AiRun",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRun_TenantId_OperationId",
                schema: "ai",
                table: "AiRun",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiRun_UniverseId_TenantId",
                schema: "ai",
                table: "AiRun",
                columns: new[] { "UniverseId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAttempt",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "AiRouteHead",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "AiProfile",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "AiRun",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "AiBudget",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "AiRoute",
                schema: "ai");
        }
    }
}
