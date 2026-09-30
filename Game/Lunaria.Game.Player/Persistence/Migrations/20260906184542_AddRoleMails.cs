using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lunaria.Game.Player.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleMails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_mails",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_id = table.Column<long>(type: "INTEGER", nullable: false),
                    mail_id = table.Column<long>(type: "INTEGER", nullable: false),
                    template_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    type = table.Column<uint>(type: "INTEGER", nullable: false),
                    important = table.Column<bool>(type: "INTEGER", nullable: false),
                    open = table.Column<bool>(type: "INTEGER", nullable: false),
                    has_attach = table.Column<bool>(type: "INTEGER", nullable: false),
                    items = table.Column<string>(type: "TEXT", nullable: false),
                    time = table.Column<long>(type: "INTEGER", nullable: false),
                    expire_time = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_mails", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_mails_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_mails_role_id",
                table: "role_mails",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_mails_role_id_mail_id",
                table: "role_mails",
                columns: new[] { "role_id", "mail_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_mails");
        }
    }
}
