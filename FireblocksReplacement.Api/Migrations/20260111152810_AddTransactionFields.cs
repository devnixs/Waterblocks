using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireblocksReplacement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerRefId",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTxId",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeCurrency",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Transactions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedAmount",
                table: "Transactions",
                type: "numeric(36,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFee",
                table: "Transactions",
                type: "numeric(36,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SubStatus",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 28, 10, 526, DateTimeKind.Utc).AddTicks(2150));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 28, 10, 526, DateTimeKind.Utc).AddTicks(2160));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 28, 10, 526, DateTimeKind.Utc).AddTicks(2160));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 28, 10, 526, DateTimeKind.Utc).AddTicks(2160));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerRefId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExternalTxId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FeeCurrency",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RequestedAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ServiceFee",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SubStatus",
                table: "Transactions");

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
        }
    }
}
