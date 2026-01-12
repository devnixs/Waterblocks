using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaces : Migration
    {
        private const string DefaultWorkspaceId = "00000000-0000-0000-0000-000000000001";
        private const string DefaultApiKeyId = "00000000-0000-0000-0000-000000000002";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    WorkspaceId = table.Column<string>(type: "character varying(50)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Workspaces",
                columns: new[] { "Id", "Name", "CreatedAt", "UpdatedAt" },
                values: new object[]
                {
                    DefaultWorkspaceId,
                    "Default",
                    new DateTime(2026, 1, 11, 18, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 1, 11, 18, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.InsertData(
                table: "ApiKeys",
                columns: new[] { "Id", "Name", "Key", "WorkspaceId", "CreatedAt", "UpdatedAt" },
                values: new object[]
                {
                    DefaultApiKeyId,
                    "Default",
                    "admin",
                    DefaultWorkspaceId,
                    new DateTime(2026, 1, 11, 18, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 1, 11, 18, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceId",
                table: "VaultAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: DefaultWorkspaceId);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceId",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: DefaultWorkspaceId);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Key",
                table: "ApiKeys",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_WorkspaceId",
                table: "ApiKeys",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_Name",
                table: "Workspaces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaultAccounts_WorkspaceId",
                table: "VaultAccounts",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_WorkspaceId",
                table: "Transactions",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Workspaces_WorkspaceId",
                table: "Transactions",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VaultAccounts_Workspaces_WorkspaceId",
                table: "VaultAccounts",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Workspaces_WorkspaceId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_VaultAccounts_Workspaces_WorkspaceId",
                table: "VaultAccounts");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_WorkspaceId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_VaultAccounts_WorkspaceId",
                table: "VaultAccounts");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "VaultAccounts");
        }
    }
}
