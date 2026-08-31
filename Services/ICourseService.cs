using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface ICourseService
{
    Task<List<CourseSummaryDto>> GetAllAsync();
    Task<CourseDto?> GetByIdAsync(int id);
    Task<CourseDto> CreateAsync(CourseCreateDto dto);
    Task<bool> UpdateAsync(int id, CourseUpdateDto dto);
    Task<(bool Success, string? Error)> DeleteAsync(int id);
    Task<CourseCleanupResult> DeleteLegacyCoursesAsync();
}

public sealed record CourseCleanupResult(
    int CoursesDeleted,
    int RoundsDeleted,
    IReadOnlyList<string> DeletedCourseNames);
