namespace BirdieBuddy.Services;

public interface IGolfNzCourseImporter
{
    Task<GolfNzImportResult> ImportAsync(long importRunId, CancellationToken cancellationToken = default);
}

public record GolfNzImportResult(
    int CoursesCreated,
    int CoursesUpdated,
    int TeesCreated,
    int TeesUpdated,
    int HolesCreated,
    int HolesUpdated,
    int RecordsDeactivated = 0);
