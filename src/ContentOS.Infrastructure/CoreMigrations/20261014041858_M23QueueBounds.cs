using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M23QueueBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                schema: "create",
                table: "PrivateCreation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadlineUtc",
                schema: "create",
                table: "PrivateCreation",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "DATEADD(minute, 10, SYSDATETIMEOFFSET())");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempts",
                schema: "create",
                table: "PrivateCreation");

            migrationBuilder.DropColumn(
                name: "DeadlineUtc",
                schema: "create",
                table: "PrivateCreation");
        }
    }
}
