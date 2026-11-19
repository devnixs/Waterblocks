using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class OneGateAccountPerWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeAccounts_WorkspaceId_GateSimulatorUserId",
                table: "ExchangeAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeAccounts_GateSimulatorUserId",
                table: "ExchangeAccounts",
                column: "GateSimulatorUserId",
                unique: true,
                filter: "\"GateSimulatorUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeAccounts_WorkspaceId_Gate",
                table: "ExchangeAccounts",
                column: "WorkspaceId",
                unique: true,
                filter: "\"ExchangeType\" = 'GATEIO'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeAccounts_GateSimulatorUserId",
                table: "ExchangeAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeAccounts_WorkspaceId_Gate",
                table: "ExchangeAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeAccounts_WorkspaceId_GateSimulatorUserId",
                table: "ExchangeAccounts",
                columns: new[] { "WorkspaceId", "GateSimulatorUserId" },
                unique: true,
                filter: "\"GateSimulatorUserId\" IS NOT NULL");
        }
    }
}
