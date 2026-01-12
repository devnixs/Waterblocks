using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationType",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "EXTERNAL");

            migrationBuilder.AddColumn<string>(
                name: "DestinationVaultAccountId",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAddress",
                table: "Transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "EXTERNAL");

            migrationBuilder.AddColumn<string>(
                name: "SourceVaultAccountId",
                table: "Transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DestinationVaultAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceAddress",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceVaultAccountId",
                table: "Transactions");
        }
    }
}
