namespace BirdieBuddy.Models;

// A golf course. Owns its tee and hole definitions
// and is referenced by every Round played on it.
public class Course
{
    public int Id { get; set; }

    // Null means a shared Golf NZ reference course; non-null means a member-owned course.
    public int? UserId { get; set; }
    public User? User { get; set; }

    // Stable identity from Golf New Zealand. Nullable for legacy local courses.
    public int? GolfNzClubId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public List<CourseTee> CourseTees { get; set; } = new();

    public List<Round> Rounds { get; set; } = new();
}