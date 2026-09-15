using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public interface IProductTelemetryService
{
    Task<ServiceResult<bool>> RecordAsync(int userId, ProductEventDto dto);
    Task<BetaMetricsDto> SummaryAsync(DateTime from);
}

public sealed class ProductTelemetryService : IProductTelemetryService
{
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
        { "draft_resumed", "hole_input_completed" };
    private readonly ApplicationDbContext _context;
    public ProductTelemetryService(ApplicationDbContext context) => _context = context;

    public async Task<ServiceResult<bool>> RecordAsync(int userId, ProductEventDto dto)
    {
        var eventType = dto.EventType ?? string.Empty;
        if (dto.ClientEventId == Guid.Empty || !AllowedEvents.Contains(eventType))
            return ServiceResult<bool>.Failure(ServiceErrors.TelemetryInvalid("Unsupported telemetry event."));
        if (eventType == "hole_input_completed" && (dto.DurationMs is < 0 or > 600_000 or null))
            return ServiceResult<bool>.Failure(ServiceErrors.TelemetryInvalid(
                "Hole input duration must be between 0 and 600000 milliseconds."));
        if (eventType == "draft_resumed" && dto.DurationMs is not null)
            return ServiceResult<bool>.Failure(ServiceErrors.TelemetryInvalid(
                "Resume events do not include a duration."));
        if (dto.RoundId is null || !await _context.Rounds.AnyAsync(r => r.Id == dto.RoundId && r.UserId == userId))
            return ServiceResult<bool>.Failure(ServiceErrors.RoundNotFound());
        if (await _context.ProductEvents.AnyAsync(e => e.UserId == userId && e.ClientEventId == dto.ClientEventId))
            return ServiceResult<bool>.Success(true);
        var productEvent = new ProductEvent { UserId = userId, RoundId = dto.RoundId,
            ClientEventId = dto.ClientEventId, EventType = eventType, DurationMs = dto.DurationMs, OccurredAt = DateTime.UtcNow };
        _context.ProductEvents.Add(productEvent);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException postgres &&
            postgres.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
            string.Equals(postgres.ConstraintName, "IX_ProductEvents_UserId_ClientEventId", StringComparison.Ordinal))
        {
            // The browser outbox may retry after a lost response. The unique
            // key makes this operation safely idempotent under a race too.
            _context.Entry(productEvent).State = EntityState.Detached;
        }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<BetaMetricsDto> SummaryAsync(DateTime from)
    {
        from = from.Kind == DateTimeKind.Utc ? from : from.ToUniversalTime();
        var rounds = await _context.Rounds.AsNoTracking().Where(r => r.StartedAt >= from)
            .Select(r => new { r.Id, r.Status }).ToListAsync();
        var events = await _context.ProductEvents.AsNoTracking().Where(e => e.OccurredAt >= from)
            .Select(e => new { e.RoundId, e.EventType, e.DurationMs }).ToListAsync();
        var completed = rounds.Count(r => r.Status == RoundStatus.Completed);
        var abandoned = rounds.Count(r => r.Status == RoundStatus.Abandoned);
        var terminal = completed + abandoned;
        var resumedIds = events.Where(e => e.EventType == "draft_resumed" && e.RoundId.HasValue)
            .Select(e => e.RoundId!.Value).Distinct().ToHashSet();
        var resumedCompleted = rounds.Count(r => r.Status == RoundStatus.Completed && resumedIds.Contains(r.Id));
        var timings = events.Where(e => e.EventType == "hole_input_completed" && e.DurationMs.HasValue)
            .Select(e => e.DurationMs!.Value).OrderBy(x => x).ToList();
        double? median = timings.Count == 0 ? null : timings.Count % 2 == 1
            ? timings[timings.Count / 2] / 1000d
            : (timings[timings.Count / 2 - 1] + timings[timings.Count / 2]) / 2000d;
        return new(from, DateTime.UtcNow, rounds.Count, completed, abandoned, Percent(completed, terminal),
            resumedIds.Count, resumedCompleted, Percent(resumedCompleted, resumedIds.Count), timings.Count, median);
    }

    private static double Percent(int value, int total) => total == 0 ? 0 : Math.Round(value * 100d / total, 2);
}
