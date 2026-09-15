using System.ComponentModel.DataAnnotations;

namespace BirdieBuddy.DTOs;

public record PracticeSessionDto(int Id, string FocusCode, string DrillTitle, int Minutes,
    DateTime StartedAt, DateTime? CompletedAt, string? Result, string? Notes);

public record PracticeSessionCreateDto(
    [param: Required, StringLength(80, MinimumLength = 1)] string? FocusCode,
    [param: Required, StringLength(160, MinimumLength = 1)] string? DrillTitle,
    [param: Range(1, 240)] int Minutes);

public record PracticeSessionCompleteDto(
    [param: StringLength(500)] string? Result,
    [param: StringLength(2000)] string? Notes);
