using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOffExchangeCollateral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollateralDirection",
                table: "Transactions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeAccountId",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeAccountName",
                table: "Transactions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExchangeApprovedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeType",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresExchangeApproval",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ExchangeAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CollateralAccountId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WorkspaceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExchangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MainAccountId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SimulatorAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SimulatorAccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MaskedApiKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeAccounts_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeMirrorEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkspaceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExchangeAccountId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExchangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SimulatorAccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeMirrorEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkspaceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Route = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeAccountAssets",
                columns: table => new
                {
                    ExchangeAccountId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CollateralAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Custody = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    Locked = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    ExchangeHold = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeAccountAssets", x => new { x.ExchangeAccountId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_ExchangeAccountAssets_ExchangeAccounts_ExchangeAccountId",
                        column: x => x.ExchangeAccountId,
                        principalTable: "ExchangeAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ExchangeAccountId",
                table: "Transactions",
                column: "ExchangeAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeAccounts_CollateralAccountId",
                table: "ExchangeAccounts",
                column: "CollateralAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeAccounts_ExchangeType_SimulatorAccountId",
                table: "ExchangeAccounts",
                columns: new[] { "ExchangeType", "SimulatorAccountId" },
                unique: true,
                filter: "\"SimulatorAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeAccounts_WorkspaceId",
                table: "ExchangeAccounts",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeMirrorEvents_Status_ExchangeType_CreatedAt",
                table: "ExchangeMirrorEvents",
                columns: new[] { "Status", "ExchangeType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeMirrorEvents_TransactionId",
                table: "ExchangeMirrorEvents",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_WorkspaceId_Route_Key",
                table: "IdempotencyRecords",
                columns: new[] { "WorkspaceId", "Route", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeAccountAssets");

            migrationBuilder.DropTable(
                name: "ExchangeMirrorEvents");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "ExchangeAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ExchangeAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CollateralDirection",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExchangeAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExchangeAccountName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExchangeApprovedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExchangeType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RequiresExchangeApproval",
                table: "Transactions");

        }
    }
}
