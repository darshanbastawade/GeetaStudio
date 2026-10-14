using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M21Talk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "talk");

            migrationBuilder.CreateTable(
                name: "TalkPolicy",
                schema: "talk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalkPolicy", x => x.Id);
                    table.UniqueConstraint("AK_TalkPolicy_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TalkPolicy_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalkPolicy_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalkSession",
                schema: "talk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remember = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetainUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalkSession", x => x.Id);
                    table.UniqueConstraint("AK_TalkSession_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TalkSession_CustomerProfile_ProfileId_TenantId",
                        columns: x => new { x.ProfileId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProfile",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalkSession_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalkSession_TalkPolicy_PolicyId_TenantId",
                        columns: x => new { x.PolicyId, x.TenantId },
                        principalSchema: "talk",
                        principalTable: "TalkPolicy",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalkSession_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalkTurn",
                schema: "talk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Question = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    Answer = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    SourcesJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    Credits = table.Column<long>(type: "bigint", nullable: false),
                    ProviderCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalkTurn", x => x.Id);
                    table.UniqueConstraint("AK_TalkTurn_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TalkTurn_CustomerReservation_ReservationId_TenantId",
                        columns: x => new { x.ReservationId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerReservation",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalkTurn_TalkSession_SessionId_TenantId",
                        columns: x => new { x.SessionId, x.TenantId },
                        principalSchema: "talk",
                        principalTable: "TalkSession",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalkTurn_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalkProblem",
                schema: "talk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalkProblem", x => x.Id);
                    table.UniqueConstraint("AK_TalkProblem_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_TalkProblem_TalkTurn_TurnId_TenantId",
                        columns: x => new { x.TurnId, x.TenantId },
                        principalSchema: "talk",
                        principalTable: "TalkTurn",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TalkProblem_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalkPolicy_TenantId_OperationId",
                schema: "talk",
                table: "TalkPolicy",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalkPolicy_TenantId_UniverseId_Key_Version",
                schema: "talk",
                table: "TalkPolicy",
                columns: new[] { "TenantId", "UniverseId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalkPolicy_UniverseId_TenantId",
                schema: "talk",
                table: "TalkPolicy",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkProblem_TenantId_OperationId",
                schema: "talk",
                table: "TalkProblem",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalkProblem_TurnId_TenantId",
                schema: "talk",
                table: "TalkProblem",
                columns: new[] { "TurnId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkSession_PolicyId_TenantId",
                schema: "talk",
                table: "TalkSession",
                columns: new[] { "PolicyId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkSession_ProfileId_TenantId",
                schema: "talk",
                table: "TalkSession",
                columns: new[] { "ProfileId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkSession_TenantId_UserId_OperationId",
                schema: "talk",
                table: "TalkSession",
                columns: new[] { "TenantId", "UserId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalkSession_UserId_TenantId",
                schema: "talk",
                table: "TalkSession",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkTurn_ReservationId_TenantId",
                schema: "talk",
                table: "TalkTurn",
                columns: new[] { "ReservationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkTurn_SessionId_TenantId",
                schema: "talk",
                table: "TalkTurn",
                columns: new[] { "SessionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkTurn_TenantId_OperationId",
                schema: "talk",
                table: "TalkTurn",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TalkProblem",
                schema: "talk");

            migrationBuilder.DropTable(
                name: "TalkTurn",
                schema: "talk");

            migrationBuilder.DropTable(
                name: "TalkSession",
                schema: "talk");

            migrationBuilder.DropTable(
                name: "TalkPolicy",
                schema: "talk");
        }
    }
}
