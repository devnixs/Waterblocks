using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exchanges",
                columns: table => new
                {
                    ExchangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SimulatorBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OneAccountPerWorkspace = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exchanges", x => x.ExchangeType);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeCurrencies",
                columns: table => new
                {
                    ExchangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeCurrencies", x => new { x.ExchangeType, x.AssetId });
                    table.ForeignKey(
                        name: "FK_ExchangeCurrencies_Exchanges_ExchangeType",
                        column: x => x.ExchangeType,
                        principalTable: "Exchanges",
                        principalColumn: "ExchangeType",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeCurrencies");

            migrationBuilder.DropTable(
                name: "Exchanges");
        }
    }
}
