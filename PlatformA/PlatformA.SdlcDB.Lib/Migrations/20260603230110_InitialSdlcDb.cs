using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlatformA.SdlcDB.Lib.Migrations
{
    /// <inheritdoc />
    public partial class InitialSdlcDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sdlc");

            migrationBuilder.CreateTable(
                name: "ai_jobs",
                schema: "sdlc",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sprint = table.Column<int>(type: "integer", nullable: true),
                    task_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    pr_url = table.Column<string>(type: "text", nullable: true),
                    test_generated = table.Column<bool>(type: "boolean", nullable: false),
                    review_completed = table.Column<bool>(type: "boolean", nullable: false),
                    adr_required = table.Column<bool>(type: "boolean", nullable: false),
                    duration_sec = table.Column<int>(type: "integer", nullable: true),
                    consume_tokens = table.Column<long>(type: "bigint", nullable: true),
                    cache_tokens = table.Column<long>(type: "bigint", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    source_json = table.Column<string>(type: "jsonb", nullable: true),
                    impact = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    inserted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_failures",
                schema: "sdlc",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<long>(type: "bigint", nullable: true),
                    failure_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    log_excerpt = table.Column<string>(type: "text", nullable: true),
                    fixable_by_ai = table.Column<bool>(type: "boolean", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    resolved = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_failures", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_failures_ai_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "sdlc",
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_job_steps",
                schema: "sdlc",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<long>(type: "bigint", nullable: false),
                    step_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_sec = table.Column<int>(type: "integer", nullable: true),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_job_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_job_steps_ai_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "sdlc",
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_model_runs",
                schema: "sdlc",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<long>(type: "bigint", nullable: true),
                    step_id = table.Column<long>(type: "bigint", nullable: true),
                    model_name = table.Column<string>(type: "text", nullable: true),
                    provider = table.Column<string>(type: "text", nullable: true),
                    input_tokens = table.Column<long>(type: "bigint", nullable: true),
                    output_tokens = table.Column<long>(type: "bigint", nullable: true),
                    cache_read_tokens = table.Column<long>(type: "bigint", nullable: true),
                    cache_creation_tokens = table.Column<long>(type: "bigint", nullable: true),
                    total_tokens = table.Column<long>(type: "bigint", nullable: true),
                    estimated_cost = table.Column<decimal>(type: "numeric", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    raw_usage = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_model_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_model_runs_ai_job_steps_step_id",
                        column: x => x.step_id,
                        principalSchema: "sdlc",
                        principalTable: "ai_job_steps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ai_model_runs_ai_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "sdlc",
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_failures_failure_type",
                schema: "sdlc",
                table: "ai_failures",
                column: "failure_type");

            migrationBuilder.CreateIndex(
                name: "ix_ai_failures_job_id",
                schema: "sdlc",
                table: "ai_failures",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_job_steps_job_id",
                schema: "sdlc",
                table: "ai_job_steps",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_job_steps_step_name",
                schema: "sdlc",
                table: "ai_job_steps",
                column: "step_name");

            migrationBuilder.CreateIndex(
                name: "ix_ai_jobs_branch",
                schema: "sdlc",
                table: "ai_jobs",
                column: "branch",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_jobs_created_at",
                schema: "sdlc",
                table: "ai_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_jobs_status",
                schema: "sdlc",
                table: "ai_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_ai_model_runs_job_id",
                schema: "sdlc",
                table: "ai_model_runs",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_model_runs_step_id",
                schema: "sdlc",
                table: "ai_model_runs",
                column: "step_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_failures",
                schema: "sdlc");

            migrationBuilder.DropTable(
                name: "ai_model_runs",
                schema: "sdlc");

            migrationBuilder.DropTable(
                name: "ai_job_steps",
                schema: "sdlc");

            migrationBuilder.DropTable(
                name: "ai_jobs",
                schema: "sdlc");
        }
    }
}
