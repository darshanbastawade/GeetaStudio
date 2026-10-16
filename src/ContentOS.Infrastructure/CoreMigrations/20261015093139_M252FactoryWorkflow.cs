using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentOS.Infrastructure.CoreMigrations
{
    /// <inheritdoc />
    public partial class M252FactoryWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowId",
                schema: "factory",
                table: "FactoryWork",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryWork_WorkflowId",
                schema: "factory",
                table: "FactoryWork",
                column: "WorkflowId",
                unique: true,
                filter: "[WorkflowId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FactoryWork_DurableWorkflow_WorkflowId",
                schema: "factory",
                table: "FactoryWork",
                column: "WorkflowId",
                principalSchema: "workflow",
                principalTable: "DurableWorkflow",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FactoryWork_DurableWorkflow_WorkflowId",
                schema: "factory",
                table: "FactoryWork");

            migrationBuilder.DropIndex(
                name: "IX_FactoryWork_WorkflowId",
                schema: "factory",
                table: "FactoryWork");

            migrationBuilder.DropColumn(
                name: "WorkflowId",
                schema: "factory",
                table: "FactoryWork");
        }
    }
}
