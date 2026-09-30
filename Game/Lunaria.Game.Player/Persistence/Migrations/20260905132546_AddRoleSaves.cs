using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lunaria.Game.Player.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_saves",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "INTEGER", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_saves", x => x.role_id);
                    table.ForeignKey(
                        name: "FK_role_saves_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_saves");
        }
    }
}
