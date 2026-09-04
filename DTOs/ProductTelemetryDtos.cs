namespace BirdieBuddy.DTOs;

public record ProductEventDto(Guid ClientEventId, string EventType, int? RoundId, int? DurationMs);
public record BetaMetricsDto(DateTime From, DateTime CapturedAt, int StartedRounds, int CompletedRounds,
    int AbandonedRounds, double CompletionRatePercent, int ResumedRounds, int ResumedRoundsCompleted,
    double ResumeCompletionRatePercent, int HoleTimings, double? MedianHoleInputSeconds);
