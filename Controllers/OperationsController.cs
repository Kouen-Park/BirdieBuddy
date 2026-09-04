using BirdieBuddy.Infrastructure;
using BirdieBuddy.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.Services;

namespace BirdieBuddy.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/operations")]
public sealed class OperationsController : ControllerBase
{
    [HttpGet]
    public ActionResult<OperationalMetricsSnapshot> Get(
        [FromServices] IOperationalMetrics metrics, [FromServices] IAdminKeyValidator adminKey)
        => adminKey.IsValid(Request) ? Ok(metrics.Snapshot()) : NotFound();

    [HttpGet("beta")]
    public async Task<ActionResult<BetaMetricsDto>> GetBeta([FromQuery] DateTime? from,
        [FromServices] IProductTelemetryService telemetry, [FromServices] IAdminKeyValidator adminKey)
    {
        if (!adminKey.IsValid(Request)) return NotFound();
        var start = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30);
        if (start < DateTime.UtcNow.AddYears(-1) || start > DateTime.UtcNow) return BadRequest();
        return Ok(await telemetry.SummaryAsync(start));
    }
}
