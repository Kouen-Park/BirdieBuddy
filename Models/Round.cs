namespace BirdieBuddy.Models;

public enum RoundStatus
{
    Draft,
    Completed,
    Abandoned
}

// A single round of golf played on a Course, made up of recorded Hole records.
public class Round
{
    public int Id { get; set; }

    // Null keeps legacy rounds private and unassigned during the ownership migration.
    public int? UserId { get; set; }
    public User? User { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    // Nullable so existing rounds can survive migration when their old tee
    // name cannot be matched to an imported CourseTee.
    public int? CourseTeeId { get; set; }
    public CourseTee? CourseTee { get; set; }

    // Preserves the old free-form tee label for legacy rounds and custom tees.
    public string? LegacyTee { get; set; }

    public DateTime Date { get; set; }

    public RoundStatus Status { get; set; } = RoundStatus.Completed;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int CurrentHole { get; set; } = 1;

    public List<Hole> Holes { get; set; } = new();
    public List<ProductEvent> ProductEvents { get; set; } = new();
}
