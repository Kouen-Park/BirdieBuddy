using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;

namespace BirdieBuddy.Controllers;

[ApiController]
[Route("api/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [HttpGet("round/{roundId:int}")]
    public async Task<ActionResult<RoundStatisticsDto>> GetRoundStatistics(int roundId)
    {
        var stats = await _statisticsService.GetRoundStatisticsAsync(roundId);
        return stats is null ? NotFound() : Ok(stats);
    }

    [HttpGet("overview")]
    public async Task<ActionResult<OverviewStatisticsDto>> GetOverview()
        => Ok(await _statisticsService.GetOverviewStatisticsAsync());
}
