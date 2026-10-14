using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M24Commerce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "commerce");

            migrationBuilder.CreateTable(
                name: "CommerceAudit",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceAudit", x => x.Id);
                    table.UniqueConstraint("AK_CommerceAudit_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceAudit_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceAudit_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceInstitution",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContractReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceInstitution", x => x.Id);
                    table.UniqueConstraint("AK_CommerceInstitution_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceInstitution_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceInstitution_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceOffer",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceOffer", x => x.Id);
                    table.UniqueConstraint("AK_CommerceOffer_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceOffer_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceProduct",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    PackageJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceProduct", x => x.Id);
                    table.UniqueConstraint("AK_CommerceProduct_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceProduct_CustomerProduct_CustomerProductId_TenantId",
                        columns: x => new { x.CustomerProductId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProduct",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceProduct_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceProduct_PrivateCreation_CreationId_TenantId",
                        columns: x => new { x.CreationId, x.TenantId },
                        principalSchema: "create",
                        principalTable: "PrivateCreation",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceProduct_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceProduct_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceRoute",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Adapter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceRoute", x => x.Id);
                    table.UniqueConstraint("AK_CommerceRoute_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceRoute_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceActivity",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceActivity", x => x.Id);
                    table.UniqueConstraint("AK_CommerceActivity_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceActivity_CommerceInstitution_InstitutionId_TenantId",
                        columns: x => new { x.InstitutionId, x.TenantId },
                        principalSchema: "commerce",
                        principalTable: "CommerceInstitution",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceActivity_CustomerProfile_ProfileId_TenantId",
                        columns: x => new { x.ProfileId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProfile",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceActivity_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceActivity_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceSeat",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cohort = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceSeat", x => x.Id);
                    table.UniqueConstraint("AK_CommerceSeat_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceSeat_CommerceInstitution_InstitutionId_TenantId",
                        columns: x => new { x.InstitutionId, x.TenantId },
                        principalSchema: "commerce",
                        principalTable: "CommerceInstitution",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceSeat_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceSeat_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceOrder",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EndsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceOrder", x => x.Id);
                    table.UniqueConstraint("AK_CommerceOrder_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceOrder_CommerceInstitution_InstitutionId_TenantId",
                        columns: x => new { x.InstitutionId, x.TenantId },
                        principalSchema: "commerce",
                        principalTable: "CommerceInstitution",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceOrder_CommerceProduct_ProductId_TenantId",
                        columns: x => new { x.ProductId, x.TenantId },
                        principalSchema: "commerce",
                        principalTable: "CommerceProduct",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceOrder_CommerceRoute_RouteId_TenantId",
                        columns: x => new { x.RouteId, x.TenantId },
                        principalSchema: "commerce",
                        principalTable: "CommerceRoute",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceOrder_CustomerOrder_PaymentOrderId_TenantId",
                        columns: x => new { x.PaymentOrderId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerOrder",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceOrder_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceOrder_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceDelivery",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Lease = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Receipt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceDelivery", x => x.Id);
                    table.UniqueConstraint("AK_CommerceDelivery_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceDelivery_CommerceOrder_OrderId_TenantId",
                        columns: x => new { x.OrderId, x.TenantId },
                        principalSchema: "commerce",
                        principalTable: "CommerceOrder",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceDelivery_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommerceEffect",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceEffect", x => x.Id);
                    table.UniqueConstraint("AK_CommerceEffect_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CommerceEffect_CommerceDelivery_DeliveryId_TenantId",
                        columns: x => new { x.DeliveryId, x.TenantId },
                        principalSchema: "commerce",
                        principalTable: "CommerceDelivery",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceEffect_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceActivity_InstitutionId_TenantId",
                schema: "commerce",
                table: "CommerceActivity",
                columns: new[] { "InstitutionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceActivity_ProfileId_TenantId",
                schema: "commerce",
                table: "CommerceActivity",
                columns: new[] { "ProfileId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceActivity_TenantId_OperationId",
                schema: "commerce",
                table: "CommerceActivity",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceActivity_UserId_TenantId",
                schema: "commerce",
                table: "CommerceActivity",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceAudit_TenantId_OperationId",
                schema: "commerce",
                table: "CommerceAudit",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceAudit_UserId_TenantId",
                schema: "commerce",
                table: "CommerceAudit",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceDelivery_OrderId",
                schema: "commerce",
                table: "CommerceDelivery",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceDelivery_OrderId_TenantId",
                schema: "commerce",
                table: "CommerceDelivery",
                columns: new[] { "OrderId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceDelivery_TenantId",
                schema: "commerce",
                table: "CommerceDelivery",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CommerceEffect_DeliveryId",
                schema: "commerce",
                table: "CommerceEffect",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceEffect_DeliveryId_TenantId",
                schema: "commerce",
                table: "CommerceEffect",
                columns: new[] { "DeliveryId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceEffect_TenantId",
                schema: "commerce",
                table: "CommerceEffect",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CommerceInstitution_TenantId_OperationId",
                schema: "commerce",
                table: "CommerceInstitution",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceInstitution_UserId_TenantId",
                schema: "commerce",
                table: "CommerceInstitution",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOffer_TenantId_OperationId",
                schema: "commerce",
                table: "CommerceOffer",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_InstitutionId_TenantId",
                schema: "commerce",
                table: "CommerceOrder",
                columns: new[] { "InstitutionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_PaymentOrderId_TenantId",
                schema: "commerce",
                table: "CommerceOrder",
                columns: new[] { "PaymentOrderId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_ProductId_TenantId",
                schema: "commerce",
                table: "CommerceOrder",
                columns: new[] { "ProductId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_RouteId_TenantId",
                schema: "commerce",
                table: "CommerceOrder",
                columns: new[] { "RouteId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_TenantId_OperationId",
                schema: "commerce",
                table: "CommerceOrder",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_UserId_TenantId",
                schema: "commerce",
                table: "CommerceOrder",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceProduct_CreationId_TenantId",
                schema: "commerce",
                table: "CommerceProduct",
                columns: new[] { "CreationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceProduct_CustomerProductId_TenantId",
                schema: "commerce",
                table: "CommerceProduct",
                columns: new[] { "CustomerProductId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceProduct_TenantId_Key_Version",
                schema: "commerce",
                table: "CommerceProduct",
                columns: new[] { "TenantId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceProduct_TenantId_OperationId",
                schema: "commerce",
                table: "CommerceProduct",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceProduct_UniverseId_TenantId",
                schema: "commerce",
                table: "CommerceProduct",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceProduct_UserId_TenantId",
                schema: "commerce",
                table: "CommerceProduct",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceRoute_TenantId",
                schema: "commerce",
                table: "CommerceRoute",
                column: "TenantId",
                unique: true,
                filter: "[Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CommerceRoute_TenantId_Key_Version",
                schema: "commerce",
                table: "CommerceRoute",
                columns: new[] { "TenantId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceRoute_TenantId_OperationId",
                schema: "commerce",
                table: "CommerceRoute",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceSeat_InstitutionId_TenantId",
                schema: "commerce",
                table: "CommerceSeat",
                columns: new[] { "InstitutionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceSeat_TenantId_InstitutionId_UserId",
                schema: "commerce",
                table: "CommerceSeat",
                columns: new[] { "TenantId", "InstitutionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommerceSeat_UserId_TenantId",
                schema: "commerce",
                table: "CommerceSeat",
                columns: new[] { "UserId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommerceActivity",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceAudit",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceEffect",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceOffer",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceSeat",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceDelivery",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceOrder",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceInstitution",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceProduct",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CommerceRoute",
                schema: "commerce");
        }
    }
}
