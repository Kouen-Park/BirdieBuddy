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
        var result = await _service.StartAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRecent), result.Value)
            : this.ApiProblem(result.Error!);
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<PracticeSessionDto>> Complete(int id, PracticeSessionCompleteDto dto)
    {
        var result = await _service.CompleteAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : this.ApiProblem(result.Error!);
    }
}
