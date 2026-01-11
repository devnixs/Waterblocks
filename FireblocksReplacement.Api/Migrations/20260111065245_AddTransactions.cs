using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireblocksReplacement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VaultAccountId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    DestinationAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DestinationTag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    NetworkFee = table.Column<decimal>(type: "numeric(36,18)", nullable: false),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReplacedByTxId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Confirmations = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_VaultAccounts_VaultAccountId",
                        column: x => x.VaultAccountId,
                        principalTable: "VaultAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 52, 45, 148, DateTimeKind.Utc).AddTicks(2060));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 52, 45, 148, DateTimeKind.Utc).AddTicks(2060));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 52, 45, 148, DateTimeKind.Utc).AddTicks(2070));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 52, 45, 148, DateTimeKind.Utc).AddTicks(2060));

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Hash",
                table: "Transactions",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_State",
                table: "Transactions",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_VaultAccountId",
                table: "Transactions",
                column: "VaultAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6520));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6530));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6530));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6530));
        }
    }
}
