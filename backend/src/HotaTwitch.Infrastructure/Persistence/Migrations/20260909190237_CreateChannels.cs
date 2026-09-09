using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotaTwitch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    channel_id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    token_hint = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_state_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.channel_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channels_token_hash",
                table: "channels",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channels");
        }
    }
}
