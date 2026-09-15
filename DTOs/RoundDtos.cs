using System.ComponentModel.DataAnnotations;

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
    [param: Range(1, int.MaxValue)]
    int CourseId,
    DateOnly Date,
    int? CourseTeeId,
    [param: StringLength(120)] string? Tee,
    [param: Required, MinLength(1)] List<HoleCreateDto>? Holes);

public record RoundUpdateDto(
    DateOnly Date,
    int? CourseTeeId,
    [param: StringLength(120)] string? Tee,
    DateTime? ExpectedUpdatedAt = null);

public record RoundStartDto(
    [param: Range(1, int.MaxValue)] int CourseId,
    DateOnly Date,
    int? CourseTeeId,
    [param: StringLength(120)] string? Tee);

public record HoleUpsertDto(
    [param: Range(3, 6)] int? Par,
    [param: Range(1, 20)] int Score,
    [param: Range(0, 10)] int Putts,
    bool GIR,
    bool? FairwayHit,
    [param: Range(0, 20)] int Penalty,
    bool CheckExpected = false, HoleDto? ExpectedHole = null);

public record RoundQueryDto(
    int? Cursor,
    [param: Range(1, 100)] int Limit = 20,
    int? CourseId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    [param: StringLength(20)] string? Status = null,
    [param: Range(1, 18)] int? HoleCount = null,
    [param: StringLength(120)] string? Search = null,
    int? CourseTeeId = null);

public record RoundPageDto(List<RoundSummaryDto> Items, int? NextCursor);

// Lightweight selector data. It deliberately excludes hole rows so filters
// do not download every recorded score just to populate a dropdown.
public record RoundOptionDto(int Id, DateOnly Date, string CourseName, int? CourseTeeId,
    string Tee, string Status, int HolesPlayed, int ExpectedHoles, int TotalScore);
