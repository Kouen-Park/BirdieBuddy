using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;

namespace BirdieBuddy.Controllers;

[ApiController]
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
        if (error is not null) return BadRequest(new { error });
        return CreatedAtAction(nameof(GetById), new { id = round!.Id }, round);
    }

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
            return error == "Round not found." ? NotFound(new { error }) : BadRequest(new { error });

        return CreatedAtAction(nameof(GetHoles), new { roundId }, hole);
    }

    [HttpPut("{roundId:int}/holes/{holeId:int}")]
    public async Task<IActionResult> UpdateHole(int roundId, int holeId, HoleUpdateDto dto)
    {
        var (success, error) = await _roundService.UpdateHoleAsync(roundId, holeId, dto);
        if (!success)
            return error == "Hole not found." ? NotFound(new { error }) : BadRequest(new { error });

        return NoContent();
    }
}
