using System.ComponentModel.DataAnnotations;

namespace BirdieBuddy.DTOs;

public record ProductEventDto(
    Guid ClientEventId,
    [param: Required, StringLength(40, MinimumLength = 1)] string? EventType,
    [param: Range(1, int.MaxValue)] int? RoundId,
    [param: Range(0, 600000)] int? DurationMs);
public record BetaMetricsDto(DateTime From, DateTime CapturedAt, int StartedRounds, int CompletedRounds,
    int AbandonedRounds, double CompletionRatePercent, int ResumedRounds, int ResumedRoundsCompleted,
    double ResumeCompletionRatePercent, int HoleTimings, double? MedianHoleInputSeconds);
