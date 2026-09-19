using System.ComponentModel.DataAnnotations;

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

// Custom is true for a course this user created, false for a shared imported
// course. Only a custom course may be edited or deleted, so a client needs this
// to avoid offering an action the server will refuse.
public record CourseDto(
    int Id,
    int? GolfNzClubId,
    string Name,
    string Location,
    List<CourseTeeDto> Tees,
    List<CourseHoleDto> Holes,
    bool Custom = false);

public record CourseSummaryDto(int Id, string Name, string Location, bool Custom = false);

public record CourseQueryDto(
    [param: StringLength(120)] string? Search = null,
    [param: Range(1, 100)] int Limit = 50,
    int? Cursor = null);

public record CoursePageDto(List<CourseSummaryDto> Items, int? NextCursor);

// Kept for the existing manual course CRUD endpoint. The service stores these
// holes under a generated "Default" CourseTee.
public record CourseHoleCreateDto(
    [param: Range(1, 18)] int HoleNumber,
    [param: Range(3, 6)] int Par,
    [param: Range(0, 1000)] int Distance);

public record CourseCreateDto(
    [param: Required, StringLength(160, MinimumLength = 2)] string? Name,
    [param: Required, StringLength(160, MinimumLength = 2)] string? Location,
    [param: Required, MinLength(1)] List<CourseHoleCreateDto>? Holes);

public record CourseUpdateDto(
    [param: Required, StringLength(160, MinimumLength = 2)] string? Name,
    [param: Required, StringLength(160, MinimumLength = 2)] string? Location);
