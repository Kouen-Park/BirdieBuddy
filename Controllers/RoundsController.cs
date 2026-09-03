using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;

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
        return round is null ? NotFound() : Ok(round);
    }

    [HttpPost]
    public async Task<ActionResult<RoundDetailDto>> Create(RoundCreateDto dto)
    {
        var (round, error) = await _roundService.CreateAsync(dto);
        if (error is not null) return Problem(detail: error, statusCode: 400, title: "Round could not be created.");
        return CreatedAtAction(nameof(GetById), new { id = round!.Id }, round);
    }

    [HttpPost("drafts")]
    public async Task<ActionResult<RoundDetailDto>> StartDraft(RoundStartDto dto)
    {
        var (round, error) = await _roundService.StartAsync(dto);
        if (error is not null) return Problem(detail: error, statusCode: 400, title: "Round could not be started.");
        return CreatedAtAction(nameof(GetById), new { id = round!.Id }, round);
    }

    [HttpPut("{roundId:int}/holes/by-number/{holeNumber:int}")]
    public async Task<ActionResult<HoleDto>> UpsertHole(int roundId, int holeNumber, HoleUpsertDto dto)
    {
        var (hole, error) = await _roundService.UpsertHoleAsync(roundId, holeNumber, dto);
        if (error is not null)
            return Problem(detail: error, statusCode: error == RoundService.ConflictMessage ? 409 : error == "Round not found." ? 404 : 400, title: "Hole could not be saved.");
        return Ok(hole);
    }

    [HttpPost("{roundId:int}/complete")]
    public async Task<ActionResult<RoundDetailDto>> Complete(int roundId)
    {
        var (round, error) = await _roundService.CompleteAsync(roundId);
        if (error is not null)
            return Problem(detail: error, statusCode: error == "Round not found." ? 404 : 400, title: "Round could not be completed.");
        return Ok(round);
    }

    [HttpPost("{roundId:int}/abandon")]
    public async Task<IActionResult> Abandon(int roundId)
        => await _roundService.AbandonAsync(roundId) ? NoContent() : NotFound();

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, RoundUpdateDto dto)
    {
        var success = await _roundService.UpdateAsync(id, dto);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _roundService.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }

    [HttpGet("{roundId:int}/holes")]
    public async Task<ActionResult<List<HoleDto>>> GetHoles(int roundId)
    {
        var holes = await _roundService.GetHolesAsync(roundId);
        return holes is null ? NotFound() : Ok(holes);
    }

    [HttpPost("{roundId:int}/holes")]
    public async Task<ActionResult<HoleDto>> AddHole(int roundId, HoleCreateDto dto)
    {
        var (hole, error) = await _roundService.AddHoleAsync(roundId, dto);
        if (error is not null)
            return Problem(detail: error, statusCode: error == "Round not found." ? 404 : 400, title: "Hole could not be added.");

        return CreatedAtAction(nameof(GetHoles), new { roundId }, hole);
    }

    [HttpPut("{roundId:int}/holes/{holeId:int}")]
    public async Task<IActionResult> UpdateHole(int roundId, int holeId, HoleUpdateDto dto)
    {
        var (success, error) = await _roundService.UpdateHoleAsync(roundId, holeId, dto);
        if (!success)
            return Problem(detail: error, statusCode: error == "Hole not found." ? 404 : 400, title: "Hole could not be updated.");

        return NoContent();
    }
}
