namespace BirdieBuddy.Models;

// A single round of golf played on a Course, made up of recorded Hole records.
public class Round
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    // Nullable so existing rounds can survive migration when their old tee
    // name cannot be matched to an imported CourseTee.
    public int? CourseTeeId { get; set; }
    public CourseTee? CourseTee { get; set; }

    // Preserves the old free-form tee label for legacy rounds and custom tees.
    public string? LegacyTee { get; set; }

    public DateTime Date { get; set; }

    public List<Hole> Holes { get; set; } = new();
}