using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    AssetId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.AssetId);
                });

            migrationBuilder.InsertData(
                table: "Assets",
                columns: new[] { "AssetId", "CreatedAt", "Decimals", "IsActive", "Name", "Symbol", "Type" },
                values: new object[,]
                {
                    { "BTC", new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6520), 8, true, "Bitcoin", "BTC", "COIN" },
                    { "ETH", new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6530), 18, true, "Ethereum", "ETH", "COIN" },
                    { "USDC", new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6530), 6, true, "USD Coin", "USDC", "TOKEN" },
                    { "USDT", new DateTime(2026, 1, 11, 6, 38, 57, 18, DateTimeKind.Utc).AddTicks(6530), 6, true, "Tether", "USDT", "TOKEN" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
