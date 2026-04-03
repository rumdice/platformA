using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PlatformA.MySqlDB.Lib.DBWebApp;

public partial class DbWebAppContext : DbContext
{
    public DbWebAppContext(DbContextOptions<DbWebAppContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ItemEntity> ItemEntity { get; set; }

    public virtual DbSet<ShopEntity> ShopEntity { get; set; }

    public virtual DbSet<UserEntity> UserEntity { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_uca1400_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<ItemEntity>(entity =>
        {
            entity.HasNoKey();

            entity.HasIndex(e => e.pid, "pk");

            entity.Property(e => e.grade).HasColumnType("int(11)");
            entity.Property(e => e.name).HasMaxLength(50);
            entity.Property(e => e.pid).HasColumnType("bigint(20)");
            entity.Property(e => e.tid).HasColumnType("bigint(20)");
            entity.Property(e => e.uid).HasColumnType("bigint(20)");
        });

        modelBuilder.Entity<ShopEntity>(entity =>
        {
            entity.HasNoKey();

            entity.HasIndex(e => e.pid, "pk");

            entity.Property(e => e.name).HasMaxLength(50);
            entity.Property(e => e.pid).HasColumnType("bigint(20)");
            entity.Property(e => e.tid).HasColumnType("bigint(20)");
            entity.Property(e => e.uid).HasColumnType("bigint(20)");
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasNoKey();

            entity.HasIndex(e => e.pid, "pk");

            entity.Property(e => e.level).HasColumnType("int(11)");
            entity.Property(e => e.name).HasMaxLength(50);
            entity.Property(e => e.pid).HasColumnType("bigint(20)");
            entity.Property(e => e.uid).HasColumnType("bigint(20)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
