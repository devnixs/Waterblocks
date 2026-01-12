using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireblocksReplacement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeConfigurationToAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TreatAsGrossAmount",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseFee",
                table: "Assets",
                type: "numeric(36,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FeeAssetId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                columns: new[] { "BaseFee", "CreatedAt", "FeeAssetId" },
                values: new object[] { 0.0001m, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6470), new TimeSpan(0, 0, 0, 0, 0)), "BTC" });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                columns: new[] { "BaseFee", "CreatedAt", "FeeAssetId" },
                values: new object[] { 0.002m, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6490), new TimeSpan(0, 0, 0, 0, 0)), "ETH" });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                columns: new[] { "BaseFee", "CreatedAt", "FeeAssetId" },
                values: new object[] { 0.005m, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6510), new TimeSpan(0, 0, 0, 0, 0)), "ETH" });

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                columns: new[] { "BaseFee", "CreatedAt", "FeeAssetId" },
                values: new object[] { 0.005m, new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6490), new TimeSpan(0, 0, 0, 0, 0)), "ETH" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TreatAsGrossAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BaseFee",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FeeAssetId",
                table: "Assets");

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2400), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2410), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2410), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 4, 45, 310, DateTimeKind.Unspecified).AddTicks(2410), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
