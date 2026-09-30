using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lunaria.Game.Player.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleMotives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_motives",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_id = table.Column<long>(type: "INTEGER", nullable: false),
                    uniq_id = table.Column<long>(type: "INTEGER", nullable: false),
                    motive_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    item_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    claim_time = table.Column<long>(type: "INTEGER", nullable: false),
                    level = table.Column<uint>(type: "INTEGER", nullable: false),
                    exp = table.Column<uint>(type: "INTEGER", nullable: false),
                    refine_level = table.Column<uint>(type: "INTEGER", nullable: false),
                    break_level = table.Column<uint>(type: "INTEGER", nullable: false),
                    locked = table.Column<bool>(type: "INTEGER", nullable: false),
                    equiped_target = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_motives", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_motives_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_motives_role_id",
                table: "role_motives",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_motives_role_id_uniq_id",
                table: "role_motives",
                columns: new[] { "role_id", "uniq_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_motives");
        }
    }
}
