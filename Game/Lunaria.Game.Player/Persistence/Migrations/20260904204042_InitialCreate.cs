using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lunaria.Game.Player.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    account_key = table.Column<string>(type: "TEXT", nullable: false),
                    userid = table.Column<string>(type: "TEXT", nullable: false),
                    channel_name = table.Column<string>(type: "TEXT", nullable: false),
                    channel_uid = table.Column<string>(type: "TEXT", nullable: false),
                    udid = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    account_id = table.Column<long>(type: "INTEGER", nullable: false),
                    slot = table.Column<long>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    second_name = table.Column<string>(type: "TEXT", nullable: false),
                    gender = table.Column<int>(type: "INTEGER", nullable: false),
                    initialized = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_minted_inst_id = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_characters",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_id = table.Column<long>(type: "INTEGER", nullable: false),
                    inst_id = table.Column<long>(type: "INTEGER", nullable: false),
                    character_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    level = table.Column<uint>(type: "INTEGER", nullable: false),
                    exp = table.Column<uint>(type: "INTEGER", nullable: false),
                    break_level = table.Column<uint>(type: "INTEGER", nullable: false),
                    motive_uniq_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_characters", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_characters_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_guides",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "INTEGER", nullable: false),
                    entries = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_guides", x => x.role_id);
                    table.ForeignKey(
                        name: "FK_role_guides_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_account_key",
                table: "accounts",
                column: "account_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_userid",
                table: "accounts",
                column: "userid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_characters_role_id",
                table: "role_characters",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_characters_role_id_inst_id",
                table: "role_characters",
                columns: new[] { "role_id", "inst_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_account_id",
                table: "roles",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_account_id_slot",
                table: "roles",
                columns: new[] { "account_id", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true,
                filter: "initialized = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_characters");

            migrationBuilder.DropTable(
                name: "role_guides");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
