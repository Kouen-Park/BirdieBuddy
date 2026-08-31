namespace BirdieBuddy.DTOs;

public record RoundSummaryDto(
    int Id,
    int CourseId,
    string CourseName,
    DateTime Date,
    int? CourseTeeId,
    string Tee,
    int TotalScore,
    int ScoreToPar);

public record RoundDetailDto(
    int Id,
    int CourseId,
    string CourseName,
    DateTime Date,
    int? CourseTeeId,
    string Tee,
    List<HoleDto> Holes);

// CourseTeeId is preferred. Tee is retained as an optional compatibility field
// so older clients can still submit a tee name while the service resolves it.
public record RoundCreateDto(
    int CourseId,
    DateTime Date,
    int? CourseTeeId,
    string? Tee,
    List<HoleCreateDto> Holes);

public record RoundUpdateDto(DateTime Date, int? CourseTeeId, string? Tee);
