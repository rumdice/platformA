using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;

namespace PlatformA.MySqlDB.Lib.DBWebApp
{
    public class DbWebAppContext : DbContext
    {
        public DbWebAppContext(DbContextOptions<DbWebAppContext> options) : base(options) { }

        // ── 테이블 ──────────────────────────────────────────
        // EFCore.NamingConventions 패키지의 UseSnakeCaseNamingConventions() 덕분에
        // DbSet 이름(PascalCase) → 테이블 이름(snake_case) 자동 변환됩니다.
        //   Players      → players
        //   PlayerStats  → player_stats
        //   MatchRecords → match_records
        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerStat> PlayerStats { get; set; }
        public DbSet<MatchRecord> MatchRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── players ─────────────────────────────────────
            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username)
                      .IsRequired()
                      .HasMaxLength(20);
                entity.HasIndex(e => e.Username)
                      .IsUnique();
                entity.Property(e => e.PasswordHash)
                      .IsRequired()
                      .HasMaxLength(255);
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

                // player → player_stats (1:1, Cascade Delete)
                entity.HasOne(e => e.Stat)
                      .WithOne(s => s.Player)
                      .HasForeignKey<PlayerStat>(s => s.PlayerId)
                      .OnDelete(DeleteBehavior.Cascade);

                // player → match_records (player1)
                entity.HasMany(e => e.MatchesAsPlayer1)
                      .WithOne(m => m.Player1)
                      .HasForeignKey(m => m.Player1Id)
                      .OnDelete(DeleteBehavior.Restrict);

                // player → match_records (player2)
                entity.HasMany(e => e.MatchesAsPlayer2)
                      .WithOne(m => m.Player2)
                      .HasForeignKey(m => m.Player2Id)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── player_stats ─────────────────────────────────
            modelBuilder.Entity<PlayerStat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PlayerId).IsUnique();
                entity.Property(e => e.TotalGames).HasDefaultValue(0);
                entity.Property(e => e.Wins).HasDefaultValue(0);
                entity.Property(e => e.Losses).HasDefaultValue(0);
                // ON UPDATE CURRENT_TIMESTAMP: MariaDB에서 ValueGeneratedOnAddOrUpdate 사용
                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                      .ValueGeneratedOnAddOrUpdate();
            });

            // ── match_records ────────────────────────────────
            modelBuilder.Entity<MatchRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                // MatchStatus enum → TINYINT
                entity.Property(e => e.Status)
                      .HasConversion<byte>()
                      .HasDefaultValue(MatchStatus.Pending);
                // winner_id: nullable FK (FK without cascade — winner could be null)
                entity.HasOne(e => e.Winner)
                      .WithMany()
                      .HasForeignKey(e => e.WinnerId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
                entity.HasIndex(e => e.Player1Id);
                entity.HasIndex(e => e.Player2Id);
                entity.HasIndex(e => e.CreatedAt);
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            });
        }
    }

    /// <summary>
    /// dotnet ef migrations add / database update 를 startup project 없이
    /// 라이브러리 단독으로 실행할 수 있도록 design-time factory를 제공합니다.
    /// UseSnakeCaseNamingConventions() 는 런타임과 동일하게 적용해야 합니다.
    /// </summary>
    public class DbWebAppContextFactory : IDesignTimeDbContextFactory<DbWebAppContext>
    {
        public DbWebAppContext CreateDbContext(string[] args)
        {
            // 환경변수 WEBAPP_DB_CONNECTION 이 설정되어 있으면 우선 사용합니다.
            // 배포 스크립트(ef_deploy_WebApp.bat / .sh)에서 이 변수를 주입합니다.
            string conn = Environment.GetEnvironmentVariable("WEBAPP_DB_CONNECTION")
                ?? "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234";
            var optionsBuilder = new DbContextOptionsBuilder<DbWebAppContext>();
            optionsBuilder.UseMySql(conn, ServerVersion.AutoDetect(conn));
            optionsBuilder.UseSnakeCaseNamingConvention(); // 런타임과 동일한 옵션 필수
            return new DbWebAppContext(optionsBuilder.Options);
        }
    }
}
