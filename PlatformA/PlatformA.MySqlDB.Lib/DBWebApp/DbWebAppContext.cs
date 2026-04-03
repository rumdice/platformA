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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_uca1400_ai_ci")
            .HasCharSet("utf8mb4");

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
            // 환경변수 LOGAPP_DB_CONNECTION 이 설정되어 있으면 우선 사용합니다.
            // 배포 스크립트(ef_deploy_LogApp.bat / .sh)에서 이 변수를 주입합니다.
            string conn = Environment.GetEnvironmentVariable("WEBAPP_DB_CONNECTION")
                ?? "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234";
            var optionsBuilder = new DbContextOptionsBuilder<DbWebAppContext>();
            optionsBuilder.UseMySql(conn, ServerVersion.AutoDetect(conn));
            return new DbWebAppContext(optionsBuilder.Options);
        }
    }
}
