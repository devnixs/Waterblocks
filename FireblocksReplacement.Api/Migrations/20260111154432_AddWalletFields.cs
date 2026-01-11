using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireblocksReplacement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockHash",
                table: "Wallets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockHeight",
                table: "Wallets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Frozen",
                table: "Wallets",
                type: "numeric(36,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Pending",
                table: "Wallets",
                type: "numeric(36,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Staked",
                table: "Wallets",
                type: "numeric(36,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 44, 32, 553, DateTimeKind.Utc).AddTicks(9140));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 44, 32, 553, DateTimeKind.Utc).AddTicks(9150));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 44, 32, 553, DateTimeKind.Utc).AddTicks(9150));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 44, 32, 553, DateTimeKind.Utc).AddTicks(9150));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockHash",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "BlockHeight",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "Frozen",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "Pending",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "Staked",
                table: "Wallets");

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
    }
}
