namespace BirdieBuddy.Models;

// A single hole definition for a specific tee on a course.
public class CourseHole
{
    public int Id { get; set; }

    public int CourseTeeId { get; set; }
    public CourseTee? CourseTee { get; set; }

    public int HoleNumber { get; set; } // 1-18
    public int Par { get; set; }        // normally 3, 4, or 5
    public int Distance { get; set; }   // metres
    public int? StrokeIndex { get; set; }

    public bool IsActive { get; set; } = true;
    public long? LastSeenImportRunId { get; set; }
    public string? SourceKey { get; set; }
}
