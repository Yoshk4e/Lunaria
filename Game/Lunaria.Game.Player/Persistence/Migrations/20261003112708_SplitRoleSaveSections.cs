using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lunaria.Game.Player.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitRoleSaveSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_save_sections",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_save_sections", x => new { x.role_id, x.name });
                    table.ForeignKey(
                        name: "FK_role_save_sections_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older versions need the full JSON document after a downgrade.
            migrationBuilder.Sql("""
                UPDATE role_saves SET state = (
                    SELECT json_group_object(name, json(state))
                    FROM role_save_sections WHERE role_id = role_saves.role_id
                ) WHERE EXISTS (SELECT 1 FROM role_save_sections WHERE role_id = role_saves.role_id);
                """);
            migrationBuilder.DropTable(
                name: "role_save_sections");
        }
    }
}
