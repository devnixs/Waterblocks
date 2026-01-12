using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireblocksReplacement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConvertDateTimeToDateTimeOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 16, 38, 40, 226, DateTimeKind.Utc).AddTicks(2110));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 16, 38, 40, 226, DateTimeKind.Utc).AddTicks(2120));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 16, 38, 40, 226, DateTimeKind.Utc).AddTicks(2120));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 16, 38, 40, 226, DateTimeKind.Utc).AddTicks(2120));
        }
    }
}
