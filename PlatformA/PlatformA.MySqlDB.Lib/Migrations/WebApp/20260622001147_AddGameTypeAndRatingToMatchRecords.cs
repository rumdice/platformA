using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformA.MySqlDB.Lib.Migrations.WebApp
{
    /// <inheritdoc />
    public partial class AddGameTypeAndRatingToMatchRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── match_records: 단일 인덱스 삭제 (이미 삭제됐을 수 있음 → IF EXISTS) ──
            migrationBuilder.Sql("ALTER TABLE `match_records` DROP INDEX IF EXISTS `ix_match_records_player1id`;");
            migrationBuilder.Sql("ALTER TABLE `match_records` DROP INDEX IF EXISTS `ix_match_records_player2id`;");

            // ── match_records: 새 컬럼 추가 ────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "game_type",
                table: "match_records",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_uca1400_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "player1rating",
                table: "match_records",
                type: "int",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "player2rating",
                table: "match_records",
                type: "int",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<string>(
                name: "room_id",
                table: "match_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_uca1400_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── match_records: 복합 인덱스 + FK 생성 ───────────────────────────
            migrationBuilder.CreateIndex(
                name: "ix_match_records_player1id_created_at",
                table: "match_records",
                columns: new[] { "player1id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_match_records_player2id_created_at",
                table: "match_records",
                columns: new[] { "player2id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "fk_match_records_players_player1id",
                table: "match_records",
                column: "player1id",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_match_records_players_player2id",
                table: "match_records",
                column: "player2id",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ── user: 레거시 테이블 정규화 (raw SQL — MySQL PK-before-AUTO_INCREMENT 제약) ──
            migrationBuilder.Sql("UPDATE `user` SET `name` = '' WHERE `name` IS NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE `user` " +
                "  MODIFY COLUMN `uid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `pid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `name` varchar(50) NOT NULL," +
                "  MODIFY COLUMN `level` int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `user` DROP INDEX IF EXISTS `ix_user_pid`;");
            migrationBuilder.Sql("ALTER TABLE `user` ADD PRIMARY KEY (`pid`);");
            migrationBuilder.Sql("ALTER TABLE `user` MODIFY COLUMN `pid` bigint NOT NULL AUTO_INCREMENT;");

            // ── shop: 레거시 테이블 정규화 ─────────────────────────────────────
            migrationBuilder.Sql("UPDATE `shop` SET `name` = '' WHERE `name` IS NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE `shop` " +
                "  MODIFY COLUMN `uid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `tid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `pid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `name` varchar(50) NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `shop` DROP INDEX IF EXISTS `ix_shop_pid`;");
            migrationBuilder.Sql("ALTER TABLE `shop` ADD PRIMARY KEY (`pid`);");
            migrationBuilder.Sql("ALTER TABLE `shop` MODIFY COLUMN `pid` bigint NOT NULL AUTO_INCREMENT;");

            // ── item: 레거시 테이블 정규화 ─────────────────────────────────────
            migrationBuilder.Sql("UPDATE `item` SET `name` = '' WHERE `name` IS NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE `item` " +
                "  MODIFY COLUMN `uid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `tid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `pid` bigint NOT NULL DEFAULT 0," +
                "  MODIFY COLUMN `name` varchar(50) NOT NULL," +
                "  MODIFY COLUMN `grade` int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `item` DROP INDEX IF EXISTS `ix_item_pid`;");
            migrationBuilder.Sql("ALTER TABLE `item` ADD PRIMARY KEY (`pid`);");
            migrationBuilder.Sql("ALTER TABLE `item` MODIFY COLUMN `pid` bigint NOT NULL AUTO_INCREMENT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── match_records: FK + 복합 인덱스 제거 ───────────────────────────
            migrationBuilder.DropForeignKey(
                name: "fk_match_records_players_player1id",
                table: "match_records");

            migrationBuilder.DropForeignKey(
                name: "fk_match_records_players_player2id",
                table: "match_records");

            migrationBuilder.DropIndex(
                name: "ix_match_records_player1id_created_at",
                table: "match_records");

            migrationBuilder.DropIndex(
                name: "ix_match_records_player2id_created_at",
                table: "match_records");

            migrationBuilder.DropColumn(name: "game_type", table: "match_records");
            migrationBuilder.DropColumn(name: "player1rating", table: "match_records");
            migrationBuilder.DropColumn(name: "player2rating", table: "match_records");
            migrationBuilder.DropColumn(name: "room_id", table: "match_records");

            migrationBuilder.CreateIndex(
                name: "ix_match_records_player1id",
                table: "match_records",
                column: "player1id");

            migrationBuilder.CreateIndex(
                name: "ix_match_records_player2id",
                table: "match_records",
                column: "player2id");

            // ── user/shop/item: 레거시 상태로 복원 (raw SQL) ───────────────────
            migrationBuilder.Sql("ALTER TABLE `user` MODIFY COLUMN `pid` bigint NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `user` DROP PRIMARY KEY;");
            migrationBuilder.Sql(
                "ALTER TABLE `user` " +
                "  MODIFY COLUMN `uid` bigint(20) NULL," +
                "  MODIFY COLUMN `pid` bigint(20) NULL," +
                "  MODIFY COLUMN `name` varchar(50) NULL," +
                "  MODIFY COLUMN `level` int(11) NULL;");
            migrationBuilder.Sql("ALTER TABLE `user` ADD INDEX `ix_user_pid` (`pid`);");

            migrationBuilder.Sql("ALTER TABLE `shop` MODIFY COLUMN `pid` bigint NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `shop` DROP PRIMARY KEY;");
            migrationBuilder.Sql(
                "ALTER TABLE `shop` " +
                "  MODIFY COLUMN `uid` bigint(20) NULL," +
                "  MODIFY COLUMN `tid` bigint(20) NULL," +
                "  MODIFY COLUMN `pid` bigint(20) NULL," +
                "  MODIFY COLUMN `name` varchar(50) NULL;");
            migrationBuilder.Sql("ALTER TABLE `shop` ADD INDEX `ix_shop_pid` (`pid`);");

            migrationBuilder.Sql("ALTER TABLE `item` MODIFY COLUMN `pid` bigint NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `item` DROP PRIMARY KEY;");
            migrationBuilder.Sql(
                "ALTER TABLE `item` " +
                "  MODIFY COLUMN `uid` bigint(20) NULL," +
                "  MODIFY COLUMN `tid` bigint(20) NULL," +
                "  MODIFY COLUMN `pid` bigint(20) NULL," +
                "  MODIFY COLUMN `name` varchar(50) NULL," +
                "  MODIFY COLUMN `grade` int(11) NULL;");
            migrationBuilder.Sql("ALTER TABLE `item` ADD INDEX `ix_item_pid` (`pid`);");
        }
    }
}
