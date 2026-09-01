using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Models;

namespace BirdieBuddy.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options
    ) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseTee> CourseTees => Set<CourseTee>();
    public DbSet<CourseHole> CourseHoles => Set<CourseHole>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Hole> Holes => Set<Hole>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder
    )
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasMaxLength(320);

        modelBuilder.Entity<User>()
            .Property(u => u.DisplayName)
            .HasMaxLength(80);

        modelBuilder.Entity<Course>()
            .HasIndex(c => c.GolfNzClubId)
            .IsUnique();

        modelBuilder.Entity<Course>()
            .HasOne(c => c.User)
            .WithMany(u => u.Courses)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Round>()
            .HasOne(r => r.User)
            .WithMany(u => u.Rounds)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseTee>()
            .HasOne(ct => ct.Course)
            .WithMany(c => c.CourseTees)
            .HasForeignKey(ct => ct.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // CourseTee -> CourseHole
        modelBuilder.Entity<CourseTee>()
            .HasIndex(ct => new { ct.CourseId, ct.CourseType, ct.Gender, ct.NineHoles, ct.Name })
            .IsUnique();

        modelBuilder.Entity<CourseHole>()
            .HasOne(ch => ch.CourseTee)
            .WithMany(ct => ct.CourseHoles)
            .HasForeignKey(ch => ch.CourseTeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseHole>()
            .HasIndex(ch => new
            {
                ch.CourseTeeId,
                ch.HoleNumber
            })
            .IsUnique();

        modelBuilder.Entity<CourseHole>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "CK_CourseHole_HoleNumber",
                    "\"HoleNumber\" BETWEEN 1 AND 18"
                )
            );

        // Course -> Round
        modelBuilder.Entity<Round>()
            .HasOne(r => r.Course)
            .WithMany(c => c.Rounds)
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Round -> CourseTee
        modelBuilder.Entity<Round>()
            .HasOne(r => r.CourseTee)
            .WithMany()
            .HasForeignKey(r => r.CourseTeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Round -> Hole
        modelBuilder.Entity<Hole>()
            .HasOne(h => h.Round)
            .WithMany(r => r.Holes)
            .HasForeignKey(h => h.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Hole>()
            .HasIndex(h => new
            {
                h.RoundId,
                h.HoleNumber
            })
            .IsUnique();

        modelBuilder.Entity<Hole>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Hole_HoleNumber",
                    "\"HoleNumber\" BETWEEN 1 AND 18"
                );

                t.HasCheckConstraint(
                    "CK_Hole_Score",
                    "\"Score\" > 0"
                );

                t.HasCheckConstraint(
                    "CK_Hole_Putts",
                    "\"Putts\" >= 0"
                );

                t.HasCheckConstraint(
                    "CK_Hole_Penalty",
                    "\"Penalty\" >= 0"
                );
            });
    }
}