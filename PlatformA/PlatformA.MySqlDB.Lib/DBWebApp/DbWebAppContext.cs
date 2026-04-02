using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;

namespace PlatformA.MySqlDB.Lib.DBWebApp
{
    public class DbWebAppContext : DbContext
    {
        public DbWebAppContext(DbContextOptions<DbWebAppContext> options) : base(options) { }

        public DbSet<Player> Players { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            });
        }
    }

    /// <summary>
    /// EF Core CLI 도구 (dotnet ef migrations add 등)가 startup project 없이
    /// 이 라이브러리 단독으로 실행될 수 있도록 design-time factory를 제공합니다.
    /// 연결 문자열은 마이그레이션 생성 전용으로만 사용됩니다.
    /// </summary>
    public class DbWebAppContextFactory : IDesignTimeDbContextFactory<DbWebAppContext>
    {
        public DbWebAppContext CreateDbContext(string[] args)
        {
            const string conn = "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234";
            var optionsBuilder = new DbContextOptionsBuilder<DbWebAppContext>();
            optionsBuilder.UseMySql(conn, ServerVersion.AutoDetect(conn));
            return new DbWebAppContext(optionsBuilder.Options);
        }
    }
}
