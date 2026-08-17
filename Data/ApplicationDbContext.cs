using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Models;

namespace BirdieBuddy.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseHole> CourseHoles => Set<CourseHole>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Hole> Holes => Set<Hole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Course -> CourseHole: deleting a course removes its hole definitions too.
        modelBuilder.Entity<CourseHole>()
            .HasOne(ch => ch.Course)
            .WithMany(c => c.CourseHoles)
            .HasForeignKey(ch => ch.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseHole>()
            .HasIndex(ch => new { ch.CourseId, ch.HoleNumber })
            .IsUnique();

        modelBuilder.Entity<CourseHole>()
            .ToTable(t => t.HasCheckConstraint("CK_CourseHole_HoleNumber", "[HoleNumber] BETWEEN 1 AND 18"));

        // Course -> Round: Restrict, not Cascade. Deleting a course must never
        // silently wipe out rounds that were actually played on it. Combined
        // with Round -> Hole (Cascade below) this also avoids SQL Server's
        // "multiple cascade paths" error, since Course would otherwise reach
        // Hole through two different cascade routes.
        modelBuilder.Entity<Round>()
            .HasOne(r => r.Course)
            .WithMany(c => c.Rounds)
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Round -> Hole: deleting a round removes its 18 hole records.
        modelBuilder.Entity<Hole>()
            .HasOne(h => h.Round)
            .WithMany(r => r.Holes)
            .HasForeignKey(h => h.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Hole>()
            .HasIndex(h => new { h.RoundId, h.HoleNumber })
            .IsUnique();

        modelBuilder.Entity<Hole>()
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_Hole_HoleNumber", "[HoleNumber] BETWEEN 1 AND 18");
                t.HasCheckConstraint("CK_Hole_Score", "[Score] > 0");
                t.HasCheckConstraint("CK_Hole_Putts", "[Putts] >= 0");
                t.HasCheckConstraint("CK_Hole_Penalty", "[Penalty] >= 0");
            });
    }
}
