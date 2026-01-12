using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockchainTypeToAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlockchainType",
                table: "Assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                columns: new[] { "BlockchainType", "CreatedAt" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2400), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                columns: new[] { "BlockchainType", "CreatedAt" },
                values: new object[] { 0, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2410), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                columns: new[] { "BlockchainType", "CreatedAt" },
                values: new object[] { 0, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2410), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                columns: new[] { "BlockchainType", "CreatedAt" },
                values: new object[] { 0, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2410), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockchainType",
                table: "Assets");

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
        }
    }
}
