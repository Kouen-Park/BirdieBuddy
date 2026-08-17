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

public record TrendPointDto(int RoundId, DateTime Date, double Value);

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
    List<TrendPointDto> PuttsTrend
);
