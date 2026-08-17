namespace BirdieBuddy.Models;

// A golf course. Owns its 18 CourseHole definitions (par/distance per hole)
// and is referenced by every Round played on it.
public class Course
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public List<CourseHole> CourseHoles { get; set; } = new();
    public List<Round> Rounds { get; set; } = new();
}
