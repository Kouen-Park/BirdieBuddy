namespace BirdieBuddy.DTOs;

public record RoundSummaryDto(
    int Id,
    int CourseId,
    string CourseName,
    DateOnly Date,
    int? CourseTeeId,
    string Tee,
    int TotalScore,
    int ScoreToPar,
    string Status = "Completed",
    int HolesPlayed = 0,
    int ExpectedHoles = 18);

public record RoundDetailDto(
    int Id,
    int CourseId,
    string CourseName,
    DateOnly Date,
    int? CourseTeeId,
    string Tee,
    List<HoleDto> Holes,
    string Status = "Completed",
    int CurrentHole = 1,
    int ExpectedHoles = 18,
    DateTime? UpdatedAt = null,
    List<int>? HoleNumbers = null);

// CourseTeeId is preferred. Tee is retained as an optional compatibility field
// so older clients can still submit a tee name while the service resolves it.
public record RoundCreateDto(
    int CourseId,
    DateOnly Date,
    int? CourseTeeId,
    string? Tee,
    List<HoleCreateDto> Holes);

public record RoundUpdateDto(DateOnly Date, int? CourseTeeId, string? Tee, DateTime? ExpectedUpdatedAt = null);

public record RoundStartDto(int CourseId, DateOnly Date, int? CourseTeeId, string? Tee);

public record HoleUpsertDto(int? Par, int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty,
    bool CheckExpected = false, HoleDto? ExpectedHole = null);

public record RoundQueryDto(
    int? Cursor,
    int Limit = 20,
    int? CourseId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Status = null,
    int? HoleCount = null,
    string? Search = null,
    int? CourseTeeId = null);

public record RoundPageDto(List<RoundSummaryDto> Items, int? NextCursor);
