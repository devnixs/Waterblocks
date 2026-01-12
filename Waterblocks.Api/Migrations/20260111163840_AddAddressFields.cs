using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressFormat",
                table: "Addresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bip44AddressIndex",
                table: "Addresses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerRefId",
                table: "Addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Addresses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnterpriseAddress",
                table: "Addresses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyAddress",
                table: "Addresses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressFormat",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Bip44AddressIndex",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "CustomerRefId",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "EnterpriseAddress",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "LegacyAddress",
                table: "Addresses");

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
    }
}
