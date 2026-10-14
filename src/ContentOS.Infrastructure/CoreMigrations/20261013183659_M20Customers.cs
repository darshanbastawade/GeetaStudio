using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M20Customers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.CreateTable(
                name: "CustomerCommand",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCommand", x => x.Id);
                    table.UniqueConstraint("AK_CustomerCommand_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerCommand_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerHousehold",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerHousehold", x => x.Id);
                    table.UniqueConstraint("AK_CustomerHousehold_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerHousehold_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentRoute",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentRoute", x => x.Id);
                    table.UniqueConstraint("AK_CustomerPaymentRoute_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerPaymentRoute_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProduct",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevenueStream = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    Credits = table.Column<long>(type: "bigint", nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    FeaturesJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProduct", x => x.Id);
                    table.UniqueConstraint("AK_CustomerProduct_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerProduct_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerProduct_Universes_UniverseId_TenantId",
                        columns: x => new { x.UniverseId, x.TenantId },
                        principalSchema: "core",
                        principalTable: "Universes",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerUser",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdultAttested = table.Column<bool>(type: "bit", nullable: false),
                    LoginHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerUser", x => x.Id);
                    table.UniqueConstraint("AK_CustomerUser_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerUser_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerChallenge",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerChallenge", x => x.Id);
                    table.UniqueConstraint("AK_CustomerChallenge_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerChallenge_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerChallenge_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerIdentity",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubjectHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerIdentity", x => x.Id);
                    table.UniqueConstraint("AK_CustomerIdentity_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerIdentity_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerIdentity_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerInvite",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInvite", x => x.Id);
                    table.UniqueConstraint("AK_CustomerInvite_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerInvite_CustomerHousehold_HouseholdId_TenantId",
                        columns: x => new { x.HouseholdId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerHousehold",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerInvite_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerInvite_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerMember",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMember", x => x.Id);
                    table.UniqueConstraint("AK_CustomerMember_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerMember_CustomerHousehold_HouseholdId_TenantId",
                        columns: x => new { x.HouseholdId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerHousehold",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerMember_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerMember_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentBinding",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentBinding", x => x.Id);
                    table.UniqueConstraint("AK_CustomerPaymentBinding_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.UniqueConstraint("AK_CustomerPaymentBinding_Id_UserId_TenantId", x => new { x.Id, x.UserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerPaymentBinding_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentBinding_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProfile",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AgeBand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Personalization = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfile", x => x.Id);
                    table.UniqueConstraint("AK_CustomerProfile_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.UniqueConstraint("AK_CustomerProfile_Id_UserId_TenantId", x => new { x.Id, x.UserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerProfile_CustomerHousehold_HouseholdId_TenantId",
                        columns: x => new { x.HouseholdId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerHousehold",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerProfile_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerProfile_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOrder",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    Acquisition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubscriptionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    RefundedMinor = table.Column<long>(type: "bigint", nullable: false),
                    EndsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaidUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Lease = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseEndsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequestedAction = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrder", x => x.Id);
                    table.UniqueConstraint("AK_CustomerOrder_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.UniqueConstraint("AK_CustomerOrder_Id_UserId_TenantId", x => new { x.Id, x.UserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerOrder_CustomerPaymentBinding_BindingId_TenantId",
                        columns: x => new { x.BindingId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerPaymentBinding",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerOrder_CustomerPaymentRoute_RouteId_TenantId",
                        columns: x => new { x.RouteId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerPaymentRoute",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerOrder_CustomerProduct_ProductId_TenantId",
                        columns: x => new { x.ProductId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProduct",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerOrder_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerOrder_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerConsent",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerConsent", x => x.Id);
                    table.UniqueConstraint("AK_CustomerConsent_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerConsent_CustomerProfile_ProfileId_TenantId",
                        columns: x => new { x.ProfileId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProfile",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerConsent_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerConsent_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProgress",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Bookmark = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProgress", x => x.Id);
                    table.UniqueConstraint("AK_CustomerProgress_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerProgress_CustomerProfile_ProfileId_TenantId",
                        columns: x => new { x.ProfileId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProfile",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerProgress_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReservation",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Resource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Consumed = table.Column<long>(type: "bigint", nullable: false),
                    OutcomeHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReservation", x => x.Id);
                    table.UniqueConstraint("AK_CustomerReservation_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.UniqueConstraint("AK_CustomerReservation_Id_UserId_TenantId", x => new { x.Id, x.UserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerReservation_CustomerProfile_ProfileId_TenantId",
                        columns: x => new { x.ProfileId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerProfile",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReservation_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReservation_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerNotification",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Receipt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotification", x => x.Id);
                    table.UniqueConstraint("AK_CustomerNotification_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerNotification_CustomerOrder_OrderId_TenantId",
                        columns: x => new { x.OrderId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerOrder",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerNotification_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerNotification_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentEvent",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentEvent", x => x.Id);
                    table.UniqueConstraint("AK_CustomerPaymentEvent_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerPaymentEvent_CustomerOrder_OrderId_TenantId",
                        columns: x => new { x.OrderId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerOrder",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentEvent_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerCredit",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCredit", x => x.Id);
                    table.UniqueConstraint("AK_CustomerCredit_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.UniqueConstraint("AK_CustomerCredit_Id_UserId_TenantId", x => new { x.Id, x.UserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CustomerCredit_CustomerCredit_LotId_UserId_TenantId",
                        columns: x => new { x.LotId, x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerCredit",
                        principalColumns: new[] { "Id", "UserId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCredit_CustomerOrder_OrderId_UserId_TenantId",
                        columns: x => new { x.OrderId, x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerOrder",
                        principalColumns: new[] { "Id", "UserId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCredit_CustomerReservation_ReservationId_UserId_TenantId",
                        columns: x => new { x.ReservationId, x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerReservation",
                        principalColumns: new[] { "Id", "UserId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCredit_CustomerUser_UserId_TenantId",
                        columns: x => new { x.UserId, x.TenantId },
                        principalSchema: "customers",
                        principalTable: "CustomerUser",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCredit_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerChallenge_TenantId",
                schema: "customers",
                table: "CustomerChallenge",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerChallenge_UserId_TenantId",
                schema: "customers",
                table: "CustomerChallenge",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCommand_TenantId_OperationId",
                schema: "customers",
                table: "CustomerCommand",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerConsent_ProfileId_TenantId",
                schema: "customers",
                table: "CustomerConsent",
                columns: new[] { "ProfileId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerConsent_TenantId",
                schema: "customers",
                table: "CustomerConsent",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerConsent_UserId_TenantId",
                schema: "customers",
                table: "CustomerConsent",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCredit_LotId_UserId_TenantId",
                schema: "customers",
                table: "CustomerCredit",
                columns: new[] { "LotId", "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCredit_OrderId_UserId_TenantId",
                schema: "customers",
                table: "CustomerCredit",
                columns: new[] { "OrderId", "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCredit_ReservationId_UserId_TenantId",
                schema: "customers",
                table: "CustomerCredit",
                columns: new[] { "ReservationId", "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCredit_TenantId",
                schema: "customers",
                table: "CustomerCredit",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCredit_UserId_TenantId",
                schema: "customers",
                table: "CustomerCredit",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerHousehold_TenantId",
                schema: "customers",
                table: "CustomerHousehold",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIdentity_TenantId_Provider_Account_Environment_SubjectHash",
                schema: "customers",
                table: "CustomerIdentity",
                columns: new[] { "TenantId", "Provider", "Account", "Environment", "SubjectHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIdentity_UserId_TenantId",
                schema: "customers",
                table: "CustomerIdentity",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvite_HouseholdId_TenantId",
                schema: "customers",
                table: "CustomerInvite",
                columns: new[] { "HouseholdId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvite_TenantId",
                schema: "customers",
                table: "CustomerInvite",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvite_UserId_TenantId",
                schema: "customers",
                table: "CustomerInvite",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMember_HouseholdId_TenantId",
                schema: "customers",
                table: "CustomerMember",
                columns: new[] { "HouseholdId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMember_TenantId_HouseholdId_UserId",
                schema: "customers",
                table: "CustomerMember",
                columns: new[] { "TenantId", "HouseholdId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMember_UserId_TenantId",
                schema: "customers",
                table: "CustomerMember",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotification_OrderId_TenantId",
                schema: "customers",
                table: "CustomerNotification",
                columns: new[] { "OrderId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotification_TenantId_Key",
                schema: "customers",
                table: "CustomerNotification",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotification_UserId_TenantId",
                schema: "customers",
                table: "CustomerNotification",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrder_BindingId_TenantId",
                schema: "customers",
                table: "CustomerOrder",
                columns: new[] { "BindingId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrder_ProductId_TenantId",
                schema: "customers",
                table: "CustomerOrder",
                columns: new[] { "ProductId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrder_RouteId_TenantId",
                schema: "customers",
                table: "CustomerOrder",
                columns: new[] { "RouteId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrder_TenantId",
                schema: "customers",
                table: "CustomerOrder",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrder_UserId_TenantId",
                schema: "customers",
                table: "CustomerOrder",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentBinding_TenantId_UserId_Provider_Account_Environment",
                schema: "customers",
                table: "CustomerPaymentBinding",
                columns: new[] { "TenantId", "UserId", "Provider", "Account", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentBinding_UserId_TenantId",
                schema: "customers",
                table: "CustomerPaymentBinding",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentEvent_OrderId_TenantId",
                schema: "customers",
                table: "CustomerPaymentEvent",
                columns: new[] { "OrderId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentEvent_TenantId_OrderId_Sequence",
                schema: "customers",
                table: "CustomerPaymentEvent",
                columns: new[] { "TenantId", "OrderId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentEvent_TenantId_Provider_Account_Environment_EventId",
                schema: "customers",
                table: "CustomerPaymentEvent",
                columns: new[] { "TenantId", "Provider", "Account", "Environment", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentRoute_TenantId",
                schema: "customers",
                table: "CustomerPaymentRoute",
                column: "TenantId",
                unique: true,
                filter: "[Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentRoute_TenantId_Key_Version",
                schema: "customers",
                table: "CustomerPaymentRoute",
                columns: new[] { "TenantId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProduct_TenantId_Key_Version",
                schema: "customers",
                table: "CustomerProduct",
                columns: new[] { "TenantId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProduct_UniverseId_TenantId",
                schema: "customers",
                table: "CustomerProduct",
                columns: new[] { "UniverseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfile_HouseholdId_TenantId",
                schema: "customers",
                table: "CustomerProfile",
                columns: new[] { "HouseholdId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfile_TenantId",
                schema: "customers",
                table: "CustomerProfile",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfile_UserId_TenantId",
                schema: "customers",
                table: "CustomerProfile",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProgress_ProfileId_TenantId",
                schema: "customers",
                table: "CustomerProgress",
                columns: new[] { "ProfileId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProgress_TenantId_ProfileId_Key",
                schema: "customers",
                table: "CustomerProgress",
                columns: new[] { "TenantId", "ProfileId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReservation_ProfileId_TenantId",
                schema: "customers",
                table: "CustomerReservation",
                columns: new[] { "ProfileId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReservation_TenantId_UserId_UsageKey",
                schema: "customers",
                table: "CustomerReservation",
                columns: new[] { "TenantId", "UserId", "UsageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReservation_UserId_TenantId",
                schema: "customers",
                table: "CustomerReservation",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUser_TenantId_LoginHash",
                schema: "customers",
                table: "CustomerUser",
                columns: new[] { "TenantId", "LoginHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerChallenge",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerCommand",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerConsent",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerCredit",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerIdentity",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerInvite",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerMember",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerNotification",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerPaymentEvent",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerProgress",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerReservation",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerOrder",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerProfile",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerPaymentBinding",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerPaymentRoute",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerProduct",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerHousehold",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerUser",
                schema: "customers");
        }
    }
}
