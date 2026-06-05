using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlatformA.SdlcDB.Lib.Entities;

namespace PlatformA.SdlcDB.Lib
{
    public sealed class SdlcDbContext : DbContext
    {
        public SdlcDbContext(DbContextOptions<SdlcDbContext> options) : base(options) { }

        public DbSet<AiJob> AiJobs => Set<AiJob>();
        public DbSet<AiJobStep> AiJobSteps => Set<AiJobStep>();
        public DbSet<AiFailure> AiFailures => Set<AiFailure>();
        public DbSet<AiModelRun> AiModelRuns => Set<AiModelRun>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // n8n이 platforma_sdlc DB의 'n8n' 스키마를 사용하므로 'sdlc' 스키마로 분리
            modelBuilder.HasDefaultSchema("sdlc");

            modelBuilder.Entity<AiJob>(entity =>
            {
                entity.ToTable("ai_jobs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Branch).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
                entity.Property(e => e.TaskName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Branch).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.SourceJson).HasColumnType("jsonb");
                entity.Property(e => e.Impact).HasColumnType("jsonb");
                entity.Property(e => e.InsertedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<AiJobStep>(entity =>
            {
                entity.ToTable("ai_job_steps");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.JobId);
                entity.HasIndex(e => e.StepName);
                entity.Property(e => e.StepName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ResultJson).HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne(e => e.Job)
                      .WithMany(e => e.Steps)
                      .HasForeignKey(e => e.JobId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AiFailure>(entity =>
            {
                entity.ToTable("ai_failures");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.JobId);
                entity.HasIndex(e => e.FailureType);
                entity.Property(e => e.FailureType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Source).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
                entity.Property(e => e.CommitSha).HasMaxLength(40);
                entity.Property(e => e.Branch).HasMaxLength(200);
                entity.HasIndex(e => new { e.GitHubRunId, e.GitHubJobId, e.FailureType })
                      .HasDatabaseName("ux_ai_failures_github_job_failure")
                      .IsUnique()
                      .HasFilter("git_hub_run_id IS NOT NULL AND git_hub_job_id IS NOT NULL");
                entity.HasIndex(e => e.Branch).HasDatabaseName("ix_ai_failures_branch");
                entity.HasIndex(e => e.CommitSha).HasDatabaseName("ix_ai_failures_commit_sha");
                entity.HasOne(e => e.Job)
                      .WithMany(e => e.Failures)
                      .HasForeignKey(e => e.JobId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AiModelRun>(entity =>
            {
                entity.ToTable("ai_model_runs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.JobId);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne(e => e.Job)
                      .WithMany(e => e.ModelRuns)
                      .HasForeignKey(e => e.JobId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Step)
                      .WithMany()
                      .HasForeignKey(e => e.StepId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }

        public sealed class Factory : IDesignTimeDbContextFactory<SdlcDbContext>
        {
            public SdlcDbContext CreateDbContext(string[] args)
            {
                var conn = Environment.GetEnvironmentVariable("SDLC_DB_CONNECTION")
                    ?? "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password";

                var optionsBuilder = new DbContextOptionsBuilder<SdlcDbContext>();
                optionsBuilder.UseNpgsql(conn)
                              .UseSnakeCaseNamingConvention();

                return new SdlcDbContext(optionsBuilder.Options);
            }
        }
    }
}
