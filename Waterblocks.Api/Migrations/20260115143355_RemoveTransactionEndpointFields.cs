using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTransactionEndpointFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Transactions\" SET \"SourceAddress\" = '' WHERE \"SourceAddress\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "SourceAddress",
                table: "Transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "DestinationType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DestinationVaultAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceVaultAccountId",
                table: "Transactions");

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 15, 14, 33, 55, 3, DateTimeKind.Unspecified).AddTicks(281), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 15, 14, 33, 55, 3, DateTimeKind.Unspecified).AddTicks(299), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 15, 14, 33, 55, 3, DateTimeKind.Unspecified).AddTicks(303), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 15, 14, 33, 55, 3, DateTimeKind.Unspecified).AddTicks(301), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationType",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DestinationVaultAccountId",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceVaultAccountId",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceAddress",
                table: "Transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6470), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6490), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6510), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 1, 12, 6, 15, 20, 181, DateTimeKind.Unspecified).AddTicks(6490), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
