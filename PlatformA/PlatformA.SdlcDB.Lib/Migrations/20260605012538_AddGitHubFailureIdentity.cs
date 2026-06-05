using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformA.SdlcDB.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubFailureIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // text→jsonb 변환은 USING 절이 필요하므로 Sql()로 직접 실행
            migrationBuilder.Sql(
                "ALTER TABLE sdlc.ai_failures ALTER COLUMN metadata TYPE jsonb USING metadata::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "branch",
                schema: "sdlc",
                table: "ai_failures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "commit_sha",
                schema: "sdlc",
                table: "ai_failures",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "git_hub_job_id",
                schema: "sdlc",
                table: "ai_failures",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "git_hub_run_id",
                schema: "sdlc",
                table: "ai_failures",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "workflow_name",
                schema: "sdlc",
                table: "ai_failures",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_failures_branch",
                schema: "sdlc",
                table: "ai_failures",
                column: "branch");

            migrationBuilder.CreateIndex(
                name: "ix_ai_failures_commit_sha",
                schema: "sdlc",
                table: "ai_failures",
                column: "commit_sha");

            migrationBuilder.CreateIndex(
                name: "ux_ai_failures_github_job_failure",
                schema: "sdlc",
                table: "ai_failures",
                columns: new[] { "git_hub_run_id", "git_hub_job_id", "failure_type" },
                unique: true,
                filter: "git_hub_run_id IS NOT NULL AND git_hub_job_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_failures_branch",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.DropIndex(
                name: "ix_ai_failures_commit_sha",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.DropIndex(
                name: "ux_ai_failures_github_job_failure",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.DropColumn(
                name: "branch",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.DropColumn(
                name: "commit_sha",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.DropColumn(
                name: "git_hub_job_id",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.DropColumn(
                name: "git_hub_run_id",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.DropColumn(
                name: "workflow_name",
                schema: "sdlc",
                table: "ai_failures");

            migrationBuilder.Sql(
                "ALTER TABLE sdlc.ai_failures ALTER COLUMN metadata TYPE text USING metadata::text");
        }
    }
}
