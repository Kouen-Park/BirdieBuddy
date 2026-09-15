using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public sealed class RoundQueryService : IRoundQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public RoundQueryService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<List<RoundSummaryDto>> GetAllAsync()
    {
        var rounds = await _context.Rounds
            .Where(r => r.UserId == CurrentUserId)
            .Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .Include(r => r.Holes)
            .OrderByDescending(r => r.Date)
            .ToListAsync();
        return rounds.Select(RoundRules.ToSummaryDto).ToList();
    }

    public async Task<List<RoundOptionDto>> GetOptionsAsync(int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 200);
        var rows = await _context.Rounds.AsNoTracking()
            .Where(r => r.UserId == CurrentUserId)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                r.Date,
                CourseName = r.Course == null ? string.Empty : r.Course.Name,
                r.CourseTeeId,
                Tee = r.LegacyTee ?? (r.CourseTee == null ? "Unknown" : r.CourseTee.Name),
                r.Status,
                HolesPlayed = r.Holes.Count,
                ExpectedHoles = r.CourseTee == null
                    ? 18
                    : (r.CourseTee.CourseHoles.Count > 0
                        ? r.CourseTee.CourseHoles.Count
                        : (r.CourseTee.NineHoles ? 9 : 18)),
                TotalScore = r.Holes.Sum(h => h.Score)
            })
            .ToListAsync();
        return rows.Select(r => new RoundOptionDto(r.Id, DateOnly.FromDateTime(r.Date), r.CourseName,
            r.CourseTeeId, r.Tee, r.Status.ToString(), r.HolesPlayed, r.ExpectedHoles, r.TotalScore)).ToList();
    }

    public async Task<RoundPageDto> GetPageAsync(RoundQueryDto query)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var rounds = _context.Rounds.AsNoTracking().Where(r => r.UserId == CurrentUserId);
        if (query.Cursor.HasValue) rounds = rounds.Where(r => r.Id < query.Cursor.Value);
        if (query.CourseId.HasValue) rounds = rounds.Where(r => r.CourseId == query.CourseId.Value);
        if (query.CourseTeeId.HasValue) rounds = rounds.Where(r => r.CourseTeeId == query.CourseTeeId.Value);
        if (query.From.HasValue) rounds = rounds.Where(r => r.Date >= RoundRules.ToUtc(query.From.Value));
        if (query.To.HasValue) rounds = rounds.Where(r => r.Date < RoundRules.ToUtc(query.To.Value).AddDays(1));
        if (query.HoleCount.HasValue) rounds = rounds.Where(r => r.Holes.Count == query.HoleCount.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rounds = rounds.Where(r => (r.Course != null && r.Course.Name.Contains(search)) ||
                (r.LegacyTee != null && r.LegacyTee.Contains(search)) ||
                (r.CourseTee != null && r.CourseTee.Name.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<RoundStatus>(query.Status, true, out var status))
            rounds = rounds.Where(r => r.Status == status);

        var items = await rounds
            .Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .Include(r => r.Holes)
            .OrderByDescending(r => r.Id)
            .Take(limit + 1)
            .ToListAsync();
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new RoundPageDto(items.Select(RoundRules.ToSummaryDto).ToList(),
            hasMore ? items[^1].Id : null);
    }

    public async Task<RoundDetailDto?> GetByIdAsync(int id)
    {
        var round = await _context.Rounds
            .Where(r => r.Id == id && r.UserId == CurrentUserId)
            .Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .Include(r => r.Holes)
            .FirstOrDefaultAsync();
        return round is null ? null : RoundRules.MapToDetailDto(round);
    }

    public async Task<List<HoleDto>?> GetHolesAsync(int roundId)
    {
        var round = await _context.Rounds
            .Where(r => r.Id == roundId && r.UserId == CurrentUserId)
            .Include(r => r.Holes)
            .FirstOrDefaultAsync();
        return round is null
            ? null
            : round.Holes.OrderBy(h => h.HoleNumber).Select(RoundRules.MapToHoleDto).ToList();
    }
}
