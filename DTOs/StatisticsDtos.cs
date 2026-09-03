namespace BirdieBuddy.DTOs;

public record ParTypeStatsDto(int HolesPlayed, double AverageScore, double AverageScoreToPar);

public record RoundStatisticsDto(
    int RoundId,
    int TotalScore,
    int ScoreToPar,
    int TotalPutts,
    double AveragePuttsPerHole,
    double GirPercentage,
    double? FairwayPercentage,
    int TotalPenalties,
    int Eagles,
    int Birdies,
    int Pars,
    int Bogeys,
    int DoubleBogeysOrWorse,
    ParTypeStatsDto Par3,
    ParTypeStatsDto Par4,
    ParTypeStatsDto Par5
);

public record TrendPointDto(int RoundId, DateOnly Date, double Value);

public record PerformanceInsightDto(string Code, string Title, string Evidence, string Recommendation, string Severity);

public record StatisticsQueryDto(int? CourseId = null, DateOnly? From = null, DateOnly? To = null, int? HoleCount = null, int? CourseTeeId = null);

public record RoundLengthStatisticsDto(int HoleCount, int RoundsPlayed, double AverageScore,
    int BestScore, double AverageScoreToPar, double? RecentFiveScoreToPar, double? RecentTenScoreToPar);

public record OverviewStatisticsDto(
    int RoundsPlayed,
    double AverageScore,
    int BestScore,
    double AveragePutts,
    double AverageGirPercentage,
    double? AverageFairwayPercentage,
    List<RoundSummaryDto> RecentRounds,
    List<TrendPointDto> ScoreTrend,
    List<TrendPointDto> GirTrend,
    List<TrendPointDto> PuttsTrend,
    double? RecentFiveScoreToPar = null,
    double? RecentTenScoreToPar = null,
    List<PerformanceInsightDto>? Insights = null,
    double AveragePuttsPerHole = 0,
    List<RoundLengthStatisticsDto>? ByRoundLength = null,
    List<TrendPointDto>? ScoreToParPerHoleTrend = null,
    List<TrendPointDto>? PuttsPerHoleTrend = null
);
