namespace BirdieBuddy.Models;

// One of the 18 holes belonging to a Course. This is the course's own
// definition of the hole (par, distance) - not tied to any specific round played.
public class CourseHole
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public int HoleNumber { get; set; } // 1-18
    public int Par { get; set; }        // normally 3, 4, or 5
    public int Distance { get; set; }   // metres
}
