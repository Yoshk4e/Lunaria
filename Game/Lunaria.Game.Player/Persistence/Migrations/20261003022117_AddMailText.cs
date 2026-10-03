using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lunaria.Game.Player.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMailText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contents",
                table: "role_mails",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "template_content_params",
                table: "role_mails",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contents",
                table: "role_mails");

            migrationBuilder.DropColumn(
                name: "template_content_params",
                table: "role_mails");
        }
    }
}
