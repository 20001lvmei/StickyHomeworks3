using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using StickyHomeworks.Core.Entities;

namespace StickyHomeworks.Core.Context;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Emotion> Emotions { get; set; }

    public virtual DbSet<EmotionsGroup> EmotionsGroups { get; set; }

    public virtual DbSet<Homework> Homeworks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={GetDatabasePath()}");
    }

    /// <summary>
    /// 数据库固定保存在用户数据目录中，不再依赖当前工作目录。
    /// 旧版本把 app.db 放在程序目录下的 db 文件夹里，这里会先把它迁移过来。
    /// </summary>
    private static string GetDatabasePath()
    {
        var dataDirectory = GetDataDirectory();
        try
        {
            Directory.CreateDirectory(dataDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[AppDbContext] 创建数据目录失败：{dataDirectory}，{ex.Message}");
            dataDirectory = Path.Combine(Path.GetTempPath(), "StickyHomeworks3");
            Directory.CreateDirectory(dataDirectory);
        }

        var target = Path.Combine(dataDirectory, "app.db");
        if (File.Exists(target))
        {
            return target;
        }

        foreach (var legacy in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "db", "app.db"),
                     Path.Combine(Environment.CurrentDirectory, "db", "app.db")
                 })
        {
            try
            {
                if (!File.Exists(legacy))
                {
                    continue;
                }

                File.Copy(legacy, target, false);
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[AppDbContext] 迁移数据库失败：{legacy}，{ex.Message}");
            }
        }

        return target;
    }

    private static string GetDataDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("STICKYHOMEWORKS3_DATA_DIR")
                         ?? Environment.GetEnvironmentVariable("STICKYHOMEWORKS_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "StickyHomeworks3");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Emotion>(entity =>
        {
            entity.ToTable("emotions");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.Path).HasColumnName("path");
            entity.Property(e => e.Size).HasColumnName("size");
        });

        modelBuilder.Entity<EmotionsGroup>(entity =>
        {
            entity.ToTable("emotions_groups");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Size)
                .HasColumnType("NUMBER")
                .HasColumnName("size");
        });

        modelBuilder.Entity<Homework>(entity =>
        {
            entity.ToTable("homework");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.EndTime)
                .HasColumnType("DATETIME")
                .HasColumnName("end_time");
            entity.Property(e => e.Subject).HasColumnName("subject");
            entity.Property(e => e.Tags).HasColumnName("tags");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
