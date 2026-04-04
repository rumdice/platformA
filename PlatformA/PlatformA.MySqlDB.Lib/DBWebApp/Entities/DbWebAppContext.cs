using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlatformA.MySqlDB.Lib.DBLogApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;

namespace PlatformA.MySqlDB.Lib.DBWebApp;

public partial class DbWebAppContext : DbContext
{
    public DbWebAppContext(DbContextOptions<DbWebAppContext> options)
        : base(options)
    {
    }

    // ── DbSet 선언 (Scaffold 엔티티 포함) ──
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Item> Items { get; set; }
    public virtual DbSet<Shop> Shops { get; set; }

    public virtual DbSet<Player> Players { get; set; }
    public virtual DbSet<PlayerStat> PlayerStats { get; set; }
    public virtual DbSet<MatchRecord> MatchRecords{ get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_uca1400_ai_ci")
            .HasCharSet("utf8mb4");

        // ════════════════════════════════════════════
        //  CF 엔티티 관계 매핑
        // ════════════════════════════════════════════

        // Player ─ 테이블/컬럼 매핑
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        });

        // PlayerStat ─ 1:1 관계 (Player ↔ PlayerStat)
        modelBuilder.Entity<PlayerStat>(entity =>
        {
            entity.ToTable("player_stats");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PlayerId).IsUnique();
            entity.Property(e => e.TotalGames).HasDefaultValue(0);
            entity.Property(e => e.Wins).HasDefaultValue(0);
            entity.Property(e => e.Losses).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasOne(e => e.Player)
                  .WithOne(p => p.Stat)
                  .HasForeignKey<PlayerStat>(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // MatchRecord ─ N:1 관계 3개 (Player1, Player2, Winner)
        modelBuilder.Entity<MatchRecord>(entity =>
        {
            entity.ToTable("match_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasDefaultValue(MatchStatus.Pending);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Player1 (필수) — MatchRecord.Player1Id → Player.MatchesAsPlayer1
            entity.HasOne(e => e.Player1)
                  .WithMany(p => p.MatchesAsPlayer1)
                  .HasForeignKey(e => e.Player1Id)
                  .OnDelete(DeleteBehavior.Restrict);

            // Player2 (필수) — MatchRecord.Player2Id → Player.MatchesAsPlayer2
            entity.HasOne(e => e.Player2)
                  .WithMany(p => p.MatchesAsPlayer2)
                  .HasForeignKey(e => e.Player2Id)
                  .OnDelete(DeleteBehavior.Restrict);

            // Winner (선택) — MatchRecord.WinnerId → Player (역방향 컬렉션 없음)
            entity.HasOne(e => e.Winner)
                  .WithMany()
                  .HasForeignKey(e => e.WinnerId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ════════════════════════════════════════════
        //  DF 엔티티 Fluent API 매핑
        // ════════════════════════════════════════════

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasNoKey().ToTable("item");
            entity.HasIndex(e => e.Pid, "pk");
            entity.Property(e => e.Grade).HasColumnType("int(11)").HasColumnName("grade");
            entity.Property(e => e.Name).HasMaxLength(50).HasColumnName("name");
            entity.Property(e => e.Pid).HasColumnType("bigint(20)").HasColumnName("pid");
            entity.Property(e => e.Tid).HasColumnType("bigint(20)").HasColumnName("tid");
            entity.Property(e => e.Uid).HasColumnType("bigint(20)").HasColumnName("uid");
        });

        modelBuilder.Entity<Shop>(entity =>
        {
            entity.HasNoKey().ToTable("shop");
            entity.HasIndex(e => e.Pid, "pk");
            entity.Property(e => e.Name).HasMaxLength(50).HasColumnName("name");
            entity.Property(e => e.Pid).HasColumnType("bigint(20)").HasColumnName("pid");
            entity.Property(e => e.Tid).HasColumnType("bigint(20)").HasColumnName("tid");
            entity.Property(e => e.Uid).HasColumnType("bigint(20)").HasColumnName("uid");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasNoKey().ToTable("user");
            entity.HasIndex(e => e.Pid, "pk");
            entity.Property(e => e.Level).HasColumnType("int(11)").HasColumnName("level");
            entity.Property(e => e.Name).HasMaxLength(50).HasColumnName("name");
            entity.Property(e => e.Pid).HasColumnType("bigint(20)").HasColumnName("pid");
            entity.Property(e => e.Uid).HasColumnType("bigint(20)").HasColumnName("uid");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    /// <summary>
    /// EF Core CLI 도구가 라이브러리 단독으로 실행될 수 있도록 design-time factory를 제공합니다.
    /// </summary>
    public class DbWebAppContextFactory : IDesignTimeDbContextFactory<DbWebAppContext>
    {
        public DbWebAppContext CreateDbContext(string[] args)
        {
            string conn = Environment.GetEnvironmentVariable("WEBAPP_DB_CONNECTION")
                ?? "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234";
            var optionsBuilder = new DbContextOptionsBuilder<DbWebAppContext>();
            optionsBuilder.UseMySql(conn, ServerVersion.AutoDetect(conn))
                          .UseSnakeCaseNamingConvention();
            return new DbWebAppContext(optionsBuilder.Options);
        }
    }
}
