using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M23Creations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "create");

            migrationBuilder.CreateTable(
                name: "CreationTemplate",
                schema: "create",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreationTemplate", x => x.Id);
                    table.UniqueConstraint("AK_CreationTemplate_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CreationTemplate_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreationTemplate_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreationUpload",
                schema: "create",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rights = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreationUpload", x => x.Id);
                    table.UniqueConstraint("AK_CreationUpload_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CreationUpload_CustomerProfile_ProfileId_TenantId",
                        columns: x => new { x.ProfileId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProfile",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreationUpload_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreationUpload_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrivateCreation",
                schema: "create",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    UploadsJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Lease = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChargedCredits = table.Column<long>(type: "bigint", nullable: false),
                    Review = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublicEligible = table.Column<bool>(type: "bit", nullable: false),
                    Cleanup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateCreation", x => x.Id);
                    table.UniqueConstraint("AK_PrivateCreation_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_PrivateCreation_CreationTemplate_TemplateId_TenantId",
                        columns: x => new { x.TemplateId, x.TenantId },
                        principalSchema: "create",
                        principalTable: "CreationTemplate",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrivateCreation_CustomerProfile_ProfileId_TenantId",
                        columns: x => new { x.ProfileId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProfile",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrivateCreation_CustomerReservation_ReservationId_UserId_TenantId",
                        columns: x => new { x.ReservationId, x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerReservation",
                        principalColumns: new[] { "Id", "UserId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrivateCreation_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrivateCreation_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreationCommand",
                schema: "create",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreationCommand", x => x.Id);
                    table.UniqueConstraint("AK_CreationCommand_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CreationCommand_PrivateCreation_CreationId_TenantId",
                        columns: x => new { x.CreationId, x.TenantId },
                        principalSchema: "create",
                        principalTable: "PrivateCreation",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreationCommand_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreationEvent",
                schema: "create",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreationEvent", x => x.Id);
                    table.UniqueConstraint("AK_CreationEvent_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CreationEvent_PrivateCreation_CreationId_TenantId",
                        columns: x => new { x.CreationId, x.TenantId },
                        principalSchema: "create",
                        principalTable: "PrivateCreation",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreationEvent_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreationRevision",
                schema: "create",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Html = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    Sha256 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreationRevision", x => x.Id);
                    table.UniqueConstraint("AK_CreationRevision_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CreationRevision_PrivateCreation_CreationId_TenantId",
                        columns: x => new { x.CreationId, x.TenantId },
                        principalSchema: "create",
                        principalTable: "PrivateCreation",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreationRevision_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreationCommand_CreationId_TenantId",
                schema: "create",
                table: "CreationCommand",
                columns: new[] { "CreationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreationCommand_TenantId_OperationId",
                schema: "create",
                table: "CreationCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreationEvent_CreationId_TenantId",
                schema: "create",
                table: "CreationEvent",
                columns: new[] { "CreationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreationEvent_TenantId",
                schema: "create",
                table: "CreationEvent",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CreationRevision_CreationId_TenantId",
                schema: "create",
                table: "CreationRevision",
                columns: new[] { "CreationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreationRevision_CreationId_Version",
                schema: "create",
                table: "CreationRevision",
                columns: new[] { "CreationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreationRevision_TenantId",
                schema: "create",
                table: "CreationRevision",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CreationTemplate_TenantId_Key_Version",
                schema: "create",
                table: "CreationTemplate",
                columns: new[] { "TenantId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreationTemplate_TenantId_OperationId",
                schema: "create",
                table: "CreationTemplate",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreationTemplate_UniverseId_TenantId",
                schema: "create",
                table: "CreationTemplate",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreationUpload_ProfileId_TenantId",
                schema: "create",
                table: "CreationUpload",
                columns: new[] { "ProfileId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreationUpload_TenantId_OperationId",
                schema: "create",
                table: "CreationUpload",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreationUpload_UserId_TenantId",
                schema: "create",
                table: "CreationUpload",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateCreation_ProfileId_TenantId",
                schema: "create",
                table: "PrivateCreation",
                columns: new[] { "ProfileId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateCreation_ReservationId_UserId_TenantId",
                schema: "create",
                table: "PrivateCreation",
                columns: new[] { "ReservationId", "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateCreation_TemplateId_TenantId",
                schema: "create",
                table: "PrivateCreation",
                columns: new[] { "TemplateId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateCreation_TenantId_OperationId",
                schema: "create",
                table: "PrivateCreation",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivateCreation_UserId_TenantId",
                schema: "create",
                table: "PrivateCreation",
                columns: new[] { "UserId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreationCommand",
                schema: "create");

            migrationBuilder.DropTable(
                name: "CreationEvent",
                schema: "create");

            migrationBuilder.DropTable(
                name: "CreationRevision",
                schema: "create");

            migrationBuilder.DropTable(
                name: "CreationUpload",
                schema: "create");

            migrationBuilder.DropTable(
                name: "PrivateCreation",
                schema: "create");

            migrationBuilder.DropTable(
                name: "CreationTemplate",
                schema: "create");
        }
    }
}
