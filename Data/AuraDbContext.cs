using AuraCoursePlanner.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace AuraCoursePlanner.Data;

public class AuraDbContext : DbContext
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuraCoursePlanner", "aura.db");

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var dir = Path.GetDirectoryName(DbPath)!;
        Directory.CreateDirectory(dir);
        options.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
            entity.HasMany(c => c.StudySessions)
                  .WithOne(s => s.Course)
                  .HasForeignKey(s => s.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudySession>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Notes).HasMaxLength(1000);
        });

        base.OnModelCreating(modelBuilder);
    }
}
