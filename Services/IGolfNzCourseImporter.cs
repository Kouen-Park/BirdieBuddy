namespace BirdieBuddy.Services;

public interface IGolfNzCourseImporter
{
    Task<GolfNzImportResult> ImportAsync(CancellationToken cancellationToken = default);
}

public record GolfNzImportResult(
    int CoursesCreated,
    int CoursesUpdated,
    int TeesCreated,
    int TeesUpdated,
    int HolesCreated,
    int HolesUpdated);
