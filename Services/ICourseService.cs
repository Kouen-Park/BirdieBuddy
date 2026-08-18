using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface ICourseService
{
    Task<List<CourseSummaryDto>> GetAllAsync();
    Task<CourseDto?> GetByIdAsync(int id);
    Task<CourseDto> CreateAsync(CourseCreateDto dto);
    Task<bool> UpdateAsync(int id, CourseUpdateDto dto);
    Task<(bool Success, string? Error)> DeleteAsync(int id);

    // Search GolfCourseAPI and import a course (with its 18 CourseHoles) into the local DB.
    Task<List<ExternalCourseSummaryDto>> SearchExternalAsync(string query);
    Task<(CourseDto? Course, string? Error)> ImportExternalAsync(int externalId, string? preferredTeeName);
}
