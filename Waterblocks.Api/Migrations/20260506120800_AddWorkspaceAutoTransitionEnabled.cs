using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Waterblocks.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceAutoTransitionEnabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoTransitionEnabled",
                table: "Workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE "Workspaces" AS w
                SET "AutoTransitionEnabled" = COALESCE(
                    (
                        SELECT CASE
                            WHEN lower(s."Value") = 'true' THEN TRUE
                            WHEN lower(s."Value") = 'false' THEN FALSE
                            ELSE NULL
                        END
                        FROM "AdminSettings" AS s
                        WHERE s."Key" = 'AutoTransitionEnabled:' || w."Id"
                        LIMIT 1
                    ),
                    (
                        SELECT CASE
                            WHEN lower(s."Value") = 'true' THEN TRUE
                            WHEN lower(s."Value") = 'false' THEN FALSE
                            ELSE NULL
                        END
                        FROM "AdminSettings" AS s
                        WHERE s."Key" = 'AutoTransitionEnabled'
                        LIMIT 1
                    ),
                    FALSE
                );
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "AdminSettings"
                WHERE "Key" = 'AutoTransitionEnabled'
                   OR "Key" LIKE 'AutoTransitionEnabled:%';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "AdminSettings"
                WHERE "Key" = 'AutoTransitionEnabled'
                   OR "Key" LIKE 'AutoTransitionEnabled:%';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "AdminSettings" ("Key", "Value", "UpdatedAt")
                SELECT
                    'AutoTransitionEnabled:' || "Id",
                    CASE WHEN "AutoTransitionEnabled" THEN 'True' ELSE 'False' END,
                    NOW()
                FROM "Workspaces";
                """);

            migrationBuilder.DropColumn(
                name: "AutoTransitionEnabled",
                table: "Workspaces");
        }
    }
}
