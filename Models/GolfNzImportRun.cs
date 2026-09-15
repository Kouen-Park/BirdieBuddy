namespace BirdieBuddy.Models;

public enum GolfNzImportStatus
{
    Running,
    Completed,
    Failed
}

public class GolfNzImportRun
{
    public long Id { get; set; }
    public string SourceName { get; set; } = "golf_nz_courses.json";
    public string SourceVersion { get; set; } = string.Empty;
    public GolfNzImportStatus Status { get; set; } = GolfNzImportStatus.Running;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int CoursesCreated { get; set; }
    public int CoursesUpdated { get; set; }
    public int TeesCreated { get; set; }
    public int TeesUpdated { get; set; }
    public int HolesCreated { get; set; }
    public int HolesUpdated { get; set; }
    public int RecordsDeactivated { get; set; }
    public string? ErrorMessage { get; set; }
}
