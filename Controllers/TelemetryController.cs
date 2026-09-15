using System.Security.Claims;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.Infrastructure;

namespace BirdieBuddy.Controllers;

[ApiController, Authorize, Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    [HttpPost("events")]
    public async Task<IActionResult> Record(ProductEventDto dto, [FromServices] IProductTelemetryService telemetry)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var result = await telemetry.RecordAsync(userId, dto);
        return result.IsSuccess ? Accepted() : this.ApiProblem(result.Error!);
    }
}
