using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface ICourseService
{
    Task<List<CourseSummaryDto>> GetAllAsync();
    Task<CoursePageDto> GetPageAsync(CourseQueryDto query);
    Task<CourseDto?> GetByIdAsync(int id);
    Task<ServiceResult<CourseDto>> CreateAsync(CourseCreateDto dto);
    Task<ServiceResult<bool>> UpdateAsync(int id, CourseUpdateDto dto);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
