using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireblocksReplacement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleWalletsPerAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_VaultAccountId_AssetId",
                table: "Wallets");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Wallets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Permanent");

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 1, 57, 605, DateTimeKind.Unspecified).AddTicks(9730), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 1, 57, 605, DateTimeKind.Unspecified).AddTicks(9730), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 1, 57, 605, DateTimeKind.Unspecified).AddTicks(9740), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 1, 57, 605, DateTimeKind.Unspecified).AddTicks(9730), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_VaultAccountId_AssetId",
                table: "Wallets",
                columns: new[] { "VaultAccountId", "AssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_VaultAccountId_AssetId",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Wallets");

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 11, 20, 33, 24, 51, DateTimeKind.Unspecified).AddTicks(3540), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 11, 20, 33, 24, 51, DateTimeKind.Unspecified).AddTicks(3550), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 11, 20, 33, 24, 51, DateTimeKind.Unspecified).AddTicks(3550), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 11, 20, 33, 24, 51, DateTimeKind.Unspecified).AddTicks(3550), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_VaultAccountId_AssetId",
                table: "Wallets",
                columns: new[] { "VaultAccountId", "AssetId" },
                unique: true);
        }
    }
}
