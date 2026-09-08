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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoundDetailDto>> GetById(int id)
    {
        var round = await _roundService.GetByIdAsync(id);
        return round is null ? this.ApiProblem(404, "round.not_found", "Round not found.") : Ok(round);
    }

    [HttpPost]
    public async Task<ActionResult<RoundDetailDto>> Create(RoundCreateDto dto)
    {
        var (round, error) = await _roundService.CreateAsync(dto);
        if (error is not null) return this.ApiProblem(400, "round.invalid", "Round could not be created.", error);
        return CreatedAtAction(nameof(GetById), new { id = round!.Id }, round);
    }

    [HttpPost("drafts")]
    public async Task<ActionResult<RoundDetailDto>> StartDraft(RoundStartDto dto)
    {
        var (round, error) = await _roundService.StartAsync(dto);
        if (error is not null) return this.ApiProblem(400, "round.invalid", "Round could not be started.", error);
        return CreatedAtAction(nameof(GetById), new { id = round!.Id }, round);
    }

    [HttpPut("{roundId:int}/holes/by-number/{holeNumber:int}")]
    public async Task<ActionResult<HoleDto>> UpsertHole(int roundId, int holeNumber, HoleUpsertDto dto)
    {
        var (hole, error) = await _roundService.UpsertHoleAsync(roundId, holeNumber, dto);
        if (error is not null)
            return this.ApiProblem(error == RoundService.ConflictMessage ? 409 : error == "Round not found." ? 404 : 400,
                error == RoundService.ConflictMessage ? "round.save_conflict" : error == "Round not found." ? "round.not_found" : "round.hole_invalid",
                "Hole could not be saved.", error);
        return Ok(hole);
    }

    [HttpPost("{roundId:int}/complete")]
    public async Task<ActionResult<RoundDetailDto>> Complete(int roundId)
    {
        var (round, error) = await _roundService.CompleteAsync(roundId);
        if (error is not null)
            return this.ApiProblem(error == "Round not found." ? 404 : 400,
                error == "Round not found." ? "round.not_found" : "round.completion_invalid",
                "Round could not be completed.", error);
        return Ok(round);
    }

    [HttpPost("{roundId:int}/abandon")]
    public async Task<IActionResult> Abandon(int roundId)
        => await _roundService.AbandonAsync(roundId) ? NoContent() : this.ApiProblem(404, "round.not_found", "Round not found.");

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, RoundUpdateDto dto)
    {
        var success = await _roundService.UpdateAsync(id, dto);
        return success ? NoContent() : this.ApiProblem(404, "round.not_found", "Round not found.");
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _roundService.DeleteAsync(id);
        return success ? NoContent() : this.ApiProblem(404, "round.not_found", "Round not found.");
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
        var (hole, error) = await _roundService.AddHoleAsync(roundId, dto);
        if (error is not null)
            return this.ApiProblem(error == "Round not found." ? 404 : 400,
                error == "Round not found." ? "round.not_found" : "round.hole_invalid", "Hole could not be added.", error);

        return CreatedAtAction(nameof(GetHoles), new { roundId }, hole);
    }

    [HttpPut("{roundId:int}/holes/{holeId:int}")]
    public async Task<IActionResult> UpdateHole(int roundId, int holeId, HoleUpdateDto dto)
    {
        var (success, error) = await _roundService.UpdateHoleAsync(roundId, holeId, dto);
        if (!success)
            return this.ApiProblem(error == RoundService.ConflictMessage ? 409 : error == "Hole not found." ? 404 : 400,
                error == RoundService.ConflictMessage ? "round.save_conflict" : error == "Hole not found." ? "round.hole_not_found" : "round.hole_invalid",
                "Hole could not be updated.", error);

        return NoContent();
    }
}
