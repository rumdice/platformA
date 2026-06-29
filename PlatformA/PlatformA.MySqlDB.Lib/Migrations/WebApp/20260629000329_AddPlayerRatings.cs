using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformA.MySqlDB.Lib.Migrations.WebApp
{
    /// <inheritdoc />
    public partial class AddPlayerRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_ratings",
                columns: table => new
                {
                    player_id = table.Column<int>(type: "int", nullable: false),
                    rating = table.Column<double>(type: "double", nullable: false, defaultValue: 1000.0),
                    win_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    lose_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    draw_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_ratings", x => x.player_id);
                    table.ForeignKey(
                        name: "fk_player_ratings_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_uca1400_ai_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_ratings");
        }
    }
}
