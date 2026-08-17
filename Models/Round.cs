namespace BirdieBuddy.Models;

// A single round of golf played on a Course, made up of 18 Hole records.
public class Round
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public DateTime Date { get; set; }
    public string Tee { get; set; } = string.Empty;

    public List<Hole> Holes { get; set; } = new();
}
