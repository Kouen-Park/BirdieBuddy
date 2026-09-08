using BirdieBuddy.DTOs;
using BirdieBuddy.Infrastructure;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BirdieBuddy.Controllers;

[ApiController]
[Authorize]
[Route("api/practice/sessions")]
public class PracticeSessionsController : ControllerBase
{
    private readonly IPracticeSessionService _service;
    public PracticeSessionsController(IPracticeSessionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<PracticeSessionDto>>> GetRecent([FromQuery] int limit = 10)
        => Ok(await _service.GetRecentAsync(limit));

    [HttpPost]
    public async Task<ActionResult<PracticeSessionDto>> Start(PracticeSessionCreateDto dto)
    {
        var (session, error) = await _service.StartAsync(dto);
        return error is null ? CreatedAtAction(nameof(GetRecent), session) : this.ApiProblem(400, "practice.invalid", "Practice session could not be started.", error);
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<PracticeSessionDto>> Complete(int id, PracticeSessionCompleteDto dto)
    {
        var (session, error) = await _service.CompleteAsync(id, dto);
        if (error is not null) return this.ApiProblem(error == "Practice session not found." ? 404 : 400, "practice.invalid", "Practice session could not be completed.", error);
        return Ok(session);
    }
}
