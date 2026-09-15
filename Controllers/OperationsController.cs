using BirdieBuddy.Infrastructure;
using BirdieBuddy.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.Services;
using BirdieBuddy.Data;
using Microsoft.EntityFrameworkCore;

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

    [HttpGet("imports")]
    public async Task<ActionResult<List<GolfNzImportRunDto>>> GetImports(
        [FromServices] ApplicationDbContext db, [FromServices] IAdminKeyValidator adminKey, [FromQuery] int limit = 20)
    {
        if (!adminKey.IsValid(Request)) return this.ApiProblem(404, "admin.not_found", "Resource not found.");
        limit = Math.Clamp(limit, 1, 100);
        return Ok(await db.GolfNzImportRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(limit)
            .Select(r => new GolfNzImportRunDto(r.Id, r.SourceName, r.SourceVersion, r.Status.ToString(), r.StartedAt,
                r.CompletedAt, r.CoursesCreated, r.CoursesUpdated, r.TeesCreated, r.TeesUpdated,
                r.HolesCreated, r.HolesUpdated, r.RecordsDeactivated, r.ErrorMessage)).ToListAsync());
    }

    [HttpGet("imports/{id:long}")]
    public async Task<ActionResult<GolfNzImportRunDto>> GetImport(long id,
        [FromServices] ApplicationDbContext db, [FromServices] IAdminKeyValidator adminKey)
    {
        if (!adminKey.IsValid(Request)) return this.ApiProblem(404, "operations.import_not_found", "Import run not found.");
        var run = await db.GolfNzImportRuns.AsNoTracking().Where(r => r.Id == id).Select(r =>
            new GolfNzImportRunDto(r.Id, r.SourceName, r.SourceVersion, r.Status.ToString(), r.StartedAt,
                r.CompletedAt, r.CoursesCreated, r.CoursesUpdated, r.TeesCreated, r.TeesUpdated,
                r.HolesCreated, r.HolesUpdated, r.RecordsDeactivated, r.ErrorMessage)).FirstOrDefaultAsync();
        return run is null ? this.ApiProblem(404, "operations.import_not_found", "Import run not found.") : Ok(run);
    }
}
