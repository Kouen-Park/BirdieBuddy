namespace BirdieBuddy.DTOs;

public record CourseHoleDto(int Id, int HoleNumber, int Par, int Distance);

public record CourseDto(int Id, string Name, string Location, List<CourseHoleDto> Holes);

public record CourseSummaryDto(int Id, string Name, string Location);

public record CourseHoleCreateDto(int HoleNumber, int Par, int Distance);

public record CourseCreateDto(string Name, string Location, List<CourseHoleCreateDto> Holes);

public record CourseUpdateDto(string Name, string Location);
