using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetAddressCaseSensitivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCaseSensitive",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE "Assets"
                SET "IsCaseSensitive" = false
                WHERE
                    ("ContractAddress" IS NOT NULL AND "ContractAddress" ILIKE '0x%')
                    OR "NativeAsset" IN ('ETH', 'MATIC_POLYGON', 'BNB_BSC', 'AVAX_C', 'BASECHAIN_ETH');
                """);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "BTC",
                column: "IsCaseSensitive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "ETH",
                column: "IsCaseSensitive",
                value: false);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDC",
                column: "IsCaseSensitive",
                value: false);

            migrationBuilder.UpdateData(
                table: "Assets",
                keyColumn: "AssetId",
                keyValue: "USDT",
                column: "IsCaseSensitive",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCaseSensitive",
                table: "Assets");
        }
    }
}
