using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Waterblocks.Api.Infrastructure.Db;

#nullable disable

namespace Waterblocks.Api.Migrations;

[DbContext(typeof(FireblocksDbContext))]
[Migration("20260917110000_AddSourceLessTransactionFields")]
public partial class AddSourceLessTransactionFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BlockHash",
            table: "Transactions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BlockHeight",
            table: "Transactions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsSourceAddressUnavailable",
            table: "Transactions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "TransactionIndex",
            table: "Transactions",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BlockHash", table: "Transactions");
        migrationBuilder.DropColumn(name: "BlockHeight", table: "Transactions");
        migrationBuilder.DropColumn(name: "IsSourceAddressUnavailable", table: "Transactions");
        migrationBuilder.DropColumn(name: "TransactionIndex", table: "Transactions");
    }
}
