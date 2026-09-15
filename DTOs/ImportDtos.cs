namespace BirdieBuddy.DTOs;

public record GolfNzImportRunDto(
    long Id, string SourceName, string SourceVersion, string Status,
    DateTime StartedAt, DateTime? CompletedAt,
    int CoursesCreated, int CoursesUpdated, int TeesCreated, int TeesUpdated,
    int HolesCreated, int HolesUpdated, int RecordsDeactivated, string? ErrorMessage);
