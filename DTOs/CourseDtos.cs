namespace BirdieBuddy.DTOs;

public record CourseHoleDto(int Id, int HoleNumber, int Par, int Distance, int? StrokeIndex);

public record CourseTeeDto(
    int Id,
    string Name,
    string CourseType,
    string Gender,
    bool NineHoles,
    decimal? Rating,
    int? Slope,
    string? Colour,
    int? TotalPar,
    int? FrontNinePar,
    int? BackNinePar,
    int? FrontNineMetres,
    int? BackNineMetres,
    List<CourseHoleDto> Holes);

public record CourseDto(
    int Id,
    int? GolfNzClubId,
    string Name,
    string Location,
    List<CourseTeeDto> Tees,
    List<CourseHoleDto> Holes);

public record CourseSummaryDto(int Id, string Name, string Location);

// Kept for the existing manual course CRUD endpoint. The service stores these
// holes under a generated "Default" CourseTee.
public record CourseHoleCreateDto(int HoleNumber, int Par, int Distance);

public record CourseCreateDto(string Name, string Location, List<CourseHoleCreateDto> Holes);

public record CourseUpdateDto(string Name, string Location);
