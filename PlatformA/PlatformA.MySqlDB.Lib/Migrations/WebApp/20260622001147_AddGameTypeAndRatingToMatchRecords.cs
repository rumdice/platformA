using Microsoft.EntityFrameworkCore.Metadata;
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
            migrationBuilder.DropIndex(
                name: "ix_user_pid",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_shop_pid",
                table: "shop");

            migrationBuilder.DropIndex(
                name: "ix_match_records_player1id",
                table: "match_records");

            migrationBuilder.DropIndex(
                name: "ix_match_records_player2id",
                table: "match_records");

            migrationBuilder.DropIndex(
                name: "ix_item_pid",
                table: "item");

            migrationBuilder.AlterColumn<long>(
                name: "uid",
                table: "user",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "pid",
                table: "user",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "name",
                keyValue: null,
                column: "name",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "user",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "utf8mb4_uca1400_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_uca1400_ai_ci");

            migrationBuilder.AlterColumn<int>(
                name: "level",
                table: "user",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int(11)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "uid",
                table: "shop",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "tid",
                table: "shop",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "pid",
                table: "shop",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.UpdateData(
                table: "shop",
                keyColumn: "name",
                keyValue: null,
                column: "name",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "shop",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "utf8mb4_uca1400_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_uca1400_ai_ci");

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

            migrationBuilder.AlterColumn<long>(
                name: "uid",
                table: "item",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "tid",
                table: "item",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "pid",
                table: "item",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint(20)",
                oldNullable: true)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.UpdateData(
                table: "item",
                keyColumn: "name",
                keyValue: null,
                column: "name",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "item",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "utf8mb4_uca1400_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_uca1400_ai_ci");

            migrationBuilder.AlterColumn<int>(
                name: "grade",
                table: "item",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int(11)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_user",
                table: "user",
                column: "pid");

            migrationBuilder.AddPrimaryKey(
                name: "pk_shop",
                table: "shop",
                column: "pid");

            migrationBuilder.AddPrimaryKey(
                name: "pk_item",
                table: "item",
                column: "pid");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_user",
                table: "user");

            migrationBuilder.DropPrimaryKey(
                name: "pk_shop",
                table: "shop");

            migrationBuilder.DropIndex(
                name: "ix_match_records_player1id_created_at",
                table: "match_records");

            migrationBuilder.DropIndex(
                name: "ix_match_records_player2id_created_at",
                table: "match_records");

            migrationBuilder.DropPrimaryKey(
                name: "pk_item",
                table: "item");

            migrationBuilder.DropColumn(
                name: "game_type",
                table: "match_records");

            migrationBuilder.DropColumn(
                name: "player1rating",
                table: "match_records");

            migrationBuilder.DropColumn(
                name: "player2rating",
                table: "match_records");

            migrationBuilder.DropColumn(
                name: "room_id",
                table: "match_records");

            migrationBuilder.AlterColumn<long>(
                name: "uid",
                table: "user",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "user",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_uca1400_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_uca1400_ai_ci");

            migrationBuilder.AlterColumn<int>(
                name: "level",
                table: "user",
                type: "int(11)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "pid",
                table: "user",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<long>(
                name: "uid",
                table: "shop",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "tid",
                table: "shop",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "shop",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_uca1400_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_uca1400_ai_ci");

            migrationBuilder.AlterColumn<long>(
                name: "pid",
                table: "shop",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<long>(
                name: "uid",
                table: "item",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "tid",
                table: "item",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "item",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_uca1400_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "utf8mb4_uca1400_ai_ci");

            migrationBuilder.AlterColumn<int>(
                name: "grade",
                table: "item",
                type: "int(11)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "pid",
                table: "item",
                type: "bigint(20)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.CreateIndex(
                name: "ix_user_pid",
                table: "user",
                column: "pid");

            migrationBuilder.CreateIndex(
                name: "ix_shop_pid",
                table: "shop",
                column: "pid");

            migrationBuilder.CreateIndex(
                name: "ix_match_records_player1id",
                table: "match_records",
                column: "player1id");

            migrationBuilder.CreateIndex(
                name: "ix_match_records_player2id",
                table: "match_records",
                column: "player2id");

            migrationBuilder.CreateIndex(
                name: "ix_item_pid",
                table: "item",
                column: "pid");
        }
    }
}
