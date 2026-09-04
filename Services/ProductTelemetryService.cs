using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public interface IProductTelemetryService
{
    Task<string?> RecordAsync(int userId, ProductEventDto dto);
    Task<BetaMetricsDto> SummaryAsync(DateTime from);
}

public sealed class ProductTelemetryService : IProductTelemetryService
{
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
        { "draft_resumed", "hole_input_completed" };
    private readonly ApplicationDbContext _context;
    public ProductTelemetryService(ApplicationDbContext context) => _context = context;

    public async Task<string?> RecordAsync(int userId, ProductEventDto dto)
    {
        if (dto.ClientEventId == Guid.Empty || !AllowedEvents.Contains(dto.EventType)) return "Unsupported telemetry event.";
        if (dto.EventType == "hole_input_completed" && (dto.DurationMs is < 0 or > 600_000 or null))
            return "Hole input duration must be between 0 and 600000 milliseconds.";
        if (dto.EventType == "draft_resumed" && dto.DurationMs is not null) return "Resume events do not include a duration.";
        if (dto.RoundId is null || !await _context.Rounds.AnyAsync(r => r.Id == dto.RoundId && r.UserId == userId))
            return "Round not found.";
        if (await _context.ProductEvents.AnyAsync(e => e.UserId == userId && e.ClientEventId == dto.ClientEventId)) return null;
        _context.ProductEvents.Add(new ProductEvent { UserId = userId, RoundId = dto.RoundId,
            ClientEventId = dto.ClientEventId, EventType = dto.EventType, DurationMs = dto.DurationMs, OccurredAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        return null;
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
