using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlatformA.MySqlDB.Lib.DBLogApp.Entities;

namespace PlatformA.MySqlDB.Lib.DBLogApp
{
    public class DbLogAppContext : DbContext
    {
        public DbLogAppContext(DbContextOptions<DbLogAppContext> options) : base(options) { }

        public DbSet<AccessLog> AccessLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccessLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IpAddress).HasMaxLength(45); // IPv6 최대 길이
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
                entity.HasIndex(e => e.PlayerId);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }

    /// <summary>
    /// EF Core CLI 도구가 라이브러리 단독으로 실행될 수 있도록 design-time factory를 제공합니다.
    /// </summary>
    public class DbLogAppContextFactory : IDesignTimeDbContextFactory<DbLogAppContext>
    {
        public DbLogAppContext CreateDbContext(string[] args)
        {
            // 환경변수 LOGAPP_DB_CONNECTION 이 설정되어 있으면 우선 사용합니다.
            // 배포 스크립트(ef_deploy_LogApp.bat / .sh)에서 이 변수를 주입합니다.
            string conn = Environment.GetEnvironmentVariable("LOGAPP_DB_CONNECTION")
                ?? "Server=localhost;Port=3306;Database=db_LogApp;User=root;Password=pass1234";
            var optionsBuilder = new DbContextOptionsBuilder<DbLogAppContext>();
            optionsBuilder.UseMySql(conn, ServerVersion.AutoDetect(conn));
            return new DbLogAppContext(optionsBuilder.Options);
        }
    }
}
