using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotaTwitch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelUiScale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ui_scale",
                table: "channels",
                type: "TEXT",
                nullable: false,
                defaultValue: "1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ui_scale",
                table: "channels");
        }
    }
}
