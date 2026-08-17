using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface IStatisticsService
{
    Task<RoundStatisticsDto?> GetRoundStatisticsAsync(int roundId);
    Task<OverviewStatisticsDto> GetOverviewStatisticsAsync();
}
