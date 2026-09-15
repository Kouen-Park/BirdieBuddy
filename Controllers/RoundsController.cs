using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;
using BirdieBuddy.Infrastructure;

namespace BirdieBuddy.Controllers;

[ApiController]
[Authorize]
[Route("api/rounds")]
public class RoundsController : ControllerBase
{
    private readonly IRoundService _roundService;

    public RoundsController(IRoundService roundService)
    {
        _roundService = roundService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoundSummaryDto>>> GetAll()
        => Ok(await _roundService.GetAllAsync());

    [HttpGet("page")]
    public async Task<ActionResult<RoundPageDto>> GetPage([FromQuery] RoundQueryDto query)
        => Ok(await _roundService.GetPageAsync(query));

    [HttpGet("options")]
    public async Task<ActionResult<List<RoundOptionDto>>> GetOptions([FromQuery] int limit = 100)
        => Ok(await _roundService.GetOptionsAsync(limit));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoundDetailDto>> GetById(int id)
    {
        var round = await _roundService.GetByIdAsync(id);
        return round is null ? this.ApiProblem(404, "round.not_found", "Round not found.") : Ok(round);
    }

    [HttpPost]
    public async Task<ActionResult<RoundDetailDto>> Create(RoundCreateDto dto)
    {
        var result = await _roundService.CreateAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ApiProblem(result.Error!);
    }

    [HttpPost("drafts")]
    public async Task<ActionResult<RoundDetailDto>> StartDraft(RoundStartDto dto)
    {
        var result = await _roundService.StartAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ApiProblem(result.Error!);
    }

    [HttpPut("{roundId:int}/holes/by-number/{holeNumber:int}")]
    public async Task<ActionResult<HoleDto>> UpsertHole(int roundId, int holeNumber, HoleUpsertDto dto)
    {
        var result = await _roundService.UpsertHoleAsync(roundId, holeNumber, dto);
        return result.IsSuccess ? Ok(result.Value) : this.ApiProblem(result.Error!);
    }

    [HttpPost("{roundId:int}/complete")]
    public async Task<ActionResult<RoundDetailDto>> Complete(int roundId)
    {
        var result = await _roundService.CompleteAsync(roundId);
        return result.IsSuccess ? Ok(result.Value) : this.ApiProblem(result.Error!);
    }

    [HttpPost("{roundId:int}/abandon")]
    public async Task<IActionResult> Abandon(int roundId)
    {
        var result = await _roundService.AbandonAsync(roundId);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, RoundUpdateDto dto)
    {
        var result = await _roundService.UpdateWithErrorAsync(id, dto);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _roundService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
    }

    [HttpGet("{roundId:int}/holes")]
    public async Task<ActionResult<List<HoleDto>>> GetHoles(int roundId)
    {
        var holes = await _roundService.GetHolesAsync(roundId);
        return holes is null ? this.ApiProblem(404, "round.not_found", "Round not found.") : Ok(holes);
    }

    [HttpPost("{roundId:int}/holes")]
    public async Task<ActionResult<HoleDto>> AddHole(int roundId, HoleCreateDto dto)
    {
        var result = await _roundService.AddHoleAsync(roundId, dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetHoles), new { roundId }, result.Value)
            : this.ApiProblem(result.Error!);
    }

    [HttpPut("{roundId:int}/holes/{holeId:int}")]
    public async Task<IActionResult> UpdateHole(int roundId, int holeId, HoleUpdateDto dto)
    {
        var result = await _roundService.UpdateHoleAsync(roundId, holeId, dto);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
    }
}
