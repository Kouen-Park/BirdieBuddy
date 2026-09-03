using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;

namespace BirdieBuddy.Controllers;

[ApiController]
[Authorize]
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
    public async Task<ActionResult<OverviewStatisticsDto>> GetOverview([FromQuery] StatisticsQueryDto query)
    {
        if (query.HoleCount.HasValue && query.HoleCount is not (9 or 18))
            return Problem(statusCode: 400, detail: "Round length must be 9 or 18 holes.");
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            return Problem(statusCode: 400, detail: "From date must be on or before To date.");
        return Ok(await _statisticsService.GetOverviewStatisticsAsync(query));
    }
}
