using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities;

public partial class DbWebAppContext : DbContext
{
    public DbWebAppContext()
    {
    }

    public DbWebAppContext(DbContextOptions<DbWebAppContext> options)
        : base(options)
    {
    }

    public virtual DbSet<EfmigrationsHistory> EfmigrationsHistories { get; set; }

    public virtual DbSet<ItemEntity> ItemEntities { get; set; }

    public virtual DbSet<ShopEntity> ShopEntities { get; set; }

    public virtual DbSet<UserEntity> UserEntities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql("server=localhost;port=3306;database=db_WebApp;user=root;password=pass1234", Microsoft.EntityFrameworkCore.ServerVersion.Parse("11.6.2-mariadb"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_uca1400_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<EfmigrationsHistory>(entity =>
        {
            entity.HasKey(e => e.MigrationId).HasName("PRIMARY");

            entity.ToTable("__EFMigrationsHistory");

            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ProductVersion).HasMaxLength(32);
        });

        modelBuilder.Entity<ItemEntity>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("ItemEntity");

            entity.HasIndex(e => e.Pid, "pk");

            entity.Property(e => e.Grade)
                .HasColumnType("int(11)")
                .HasColumnName("grade");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Tid)
                .HasColumnType("bigint(20)")
                .HasColumnName("tid");
            entity.Property(e => e.Uid)
                .HasColumnType("bigint(20)")
                .HasColumnName("uid");
        });

        modelBuilder.Entity<ShopEntity>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("ShopEntity");

            entity.HasIndex(e => e.Pid, "pk");

            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Tid)
                .HasColumnType("bigint(20)")
                .HasColumnName("tid");
            entity.Property(e => e.Uid)
                .HasColumnType("bigint(20)")
                .HasColumnName("uid");
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("UserEntity");

            entity.HasIndex(e => e.Pid, "pk");

            entity.Property(e => e.Level)
                .HasColumnType("int(11)")
                .HasColumnName("level");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.Pid)
                .HasColumnType("bigint(20)")
                .HasColumnName("pid");
            entity.Property(e => e.Uid)
                .HasColumnType("bigint(20)")
                .HasColumnName("uid");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
