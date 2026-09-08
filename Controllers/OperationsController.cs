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
        => adminKey.IsValid(Request) ? Ok(metrics.Snapshot()) : this.ApiProblem(404, "admin.not_found", "Resource not found.");

    [HttpGet("beta")]
    public async Task<ActionResult<BetaMetricsDto>> GetBeta([FromQuery] DateTime? from,
        [FromServices] IProductTelemetryService telemetry, [FromServices] IAdminKeyValidator adminKey)
    {
        if (!adminKey.IsValid(Request)) return this.ApiProblem(404, "admin.not_found", "Resource not found.");
        var start = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30);
        if (start < DateTime.UtcNow.AddYears(-1) || start > DateTime.UtcNow)
            return this.ApiProblem(400, "operations.invalid_range", "Metrics range is invalid.");
        return Ok(await telemetry.SummaryAsync(start));
    }
}
