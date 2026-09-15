using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public class StatisticsService : IStatisticsService
{
    private const int DefaultLookbackDays = 365;
    private const int MaximumLookbackDays = 365 * 5;
    private const int MaximumOverviewRounds = 2000;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public StatisticsService(ApplicationDbContext context, ICurrentUser currentUser)
    { _context = context; _currentUser = currentUser; }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<RoundStatisticsDto?> GetRoundStatisticsAsync(int roundId)
    {
        var round = await _context.Rounds.Where(r => r.Id == roundId && r.UserId == CurrentUserId)
            .Include(r => r.Holes).FirstOrDefaultAsync();
        return round is null ? null : BuildRoundStatistics(roundId, round.Holes);
    }

    public async Task<OverviewStatisticsDto> GetOverviewStatisticsAsync(StatisticsQueryDto? query = null)
    {
        query ??= new StatisticsQueryDto();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-DefaultLookbackDays + 1);
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber + 1 > MaximumLookbackDays)
            from = to.AddDays(-MaximumLookbackDays + 1);

        var filtered = _context.Rounds.AsNoTracking()
            .Where(r => r.UserId == CurrentUserId && r.Status == RoundStatus.Completed && r.Holes.Any())
            .Where(r => r.Date >= RoundRules.ToUtc(from) && r.Date < RoundRules.ToUtc(to).AddDays(1));
        if (query.CourseId.HasValue) filtered = filtered.Where(r => r.CourseId == query.CourseId.Value);
        if (query.CourseTeeId.HasValue) filtered = filtered.Where(r => r.CourseTeeId == query.CourseTeeId.Value);
        if (query.HoleCount.HasValue) filtered = filtered.Where(r => r.Holes.Count == query.HoleCount.Value);

        // Overview data is one SQL-projected row per round. Hole entities are
        // intentionally limited to the recent evidence window below.
        var rows = await filtered.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)
            .Take(MaximumOverviewRounds).Select(r => new RoundMetricRow
            {
                Id = r.Id, CourseId = r.CourseId,
                CourseName = r.Course == null ? string.Empty : r.Course.Name,
                CourseTeeId = r.CourseTeeId,
                Tee = r.LegacyTee ?? (r.CourseTee == null ? "Unknown" : r.CourseTee.Name),
                Date = r.Date, HoleCount = r.Holes.Count,
                TotalScore = r.Holes.Sum(h => h.Score), TotalPar = r.Holes.Sum(h => h.Par),
                TotalPutts = r.Holes.Sum(h => h.Putts), GirCount = r.Holes.Count(h => h.GIR),
                FairwayEligible = r.Holes.Count(h => h.Par != 3 && h.FairwayHit.HasValue),
                FairwayHits = r.Holes.Count(h => h.Par != 3 && h.FairwayHit == true)
            }).ToListAsync();

        if (rows.Count == 0) return EmptyOverview();
        var roundIds = rows.Select(r => r.Id).ToList();
        var parRows = await _context.Holes.AsNoTracking().Where(h => roundIds.Contains(h.RoundId))
            .GroupBy(h => new { h.RoundId, h.Par }).Select(g => new ParMetricRow
            {
                RoundId = g.Key.RoundId, Par = g.Key.Par, Holes = g.Count(),
                ScoreToPar = g.Sum(h => h.Score - h.Par), GirCount = g.Count(h => h.GIR)
            }).ToListAsync();

        var evidenceIds = rows.Take(5).Select(r => r.Id).ToList();
        var evidenceRounds = await _context.Rounds.AsNoTracking().Where(r => evidenceIds.Contains(r.Id))
            .Include(r => r.Holes).ToListAsync();
        var evidenceById = evidenceRounds.ToDictionary(r => r.Id);
        var insights = PracticeInsights.Build(rows.Take(5).Where(r => evidenceById.ContainsKey(r.Id))
            .Select(r => evidenceById[r.Id]));

        var recent = rows.Take(5).Select(ToSummary).ToList();
        var chronological = rows.OrderBy(r => r.Date).ThenBy(r => r.Id).ToList();
        var totalHoles = rows.Sum(r => r.HoleCount);
        var fairwayTotal = rows.Sum(r => r.FairwayEligible);
        double? avgFairway = fairwayTotal == 0 ? null : 100.0 * rows.Sum(r => r.FairwayHits) / fairwayTotal;
        var groups = rows.GroupBy(r => r.HoleCount).OrderBy(g => g.Key).Select(g =>
            new RoundLengthStatisticsDto(g.Key, g.Count(), g.Average(r => r.TotalScore), g.Min(r => r.TotalScore),
                g.Average(r => r.ScoreToPar), g.Count() >= 5 ? g.Take(5).Average(r => r.ScoreToPar) : null,
                g.Count() >= 10 ? g.Take(10).Average(r => r.ScoreToPar) : null)).ToList();
        var parLookup = parRows.ToLookup(r => r.RoundId);
        var parTrend = chronological.SelectMany(r => parLookup[r.Id].Select(p =>
            new ParTypeTrendPointDto(DateOnly.FromDateTime(r.Date), p.Par,
                (double)p.ScoreToPar / p.Holes, 100.0 * p.GirCount / p.Holes))).ToList();
        var legacyFive = groups.Count == 1 ? groups[0].RecentFiveScoreToPar : null;
        var legacyTen = groups.Count == 1 ? groups[0].RecentTenScoreToPar : null;

        return new OverviewStatisticsDto(rows.Count, rows.Average(r => r.TotalScore), rows.Min(r => r.TotalScore),
            rows.Average(r => r.TotalPutts), 100.0 * rows.Sum(r => r.GirCount) / totalHoles, avgFairway,
            recent,
            chronological.Select(r => new TrendPointDto(r.Id, DateOnly.FromDateTime(r.Date), r.TotalScore)).ToList(),
            chronological.Select(r => new TrendPointDto(r.Id, DateOnly.FromDateTime(r.Date), 100.0 * r.GirCount / r.HoleCount)).ToList(),
            chronological.Select(r => new TrendPointDto(r.Id, DateOnly.FromDateTime(r.Date), r.TotalPutts)).ToList(),
            legacyFive, legacyTen, insights, (double)rows.Sum(r => r.TotalPutts) / totalHoles, groups,
            chronological.Select(r => new TrendPointDto(r.Id, DateOnly.FromDateTime(r.Date), (double)r.ScoreToPar / r.HoleCount)).ToList(),
            chronological.Select(r => new TrendPointDto(r.Id, DateOnly.FromDateTime(r.Date), (double)r.TotalPutts / r.HoleCount)).ToList(),
            BuildMovingAverages(chronological), parTrend);
    }

    private static OverviewStatisticsDto EmptyOverview() => new(0, 0, 0, 0, 0, null,
        new(), new(), new(), new(), null, null, PracticeInsights.Build(new List<Round>()), 0, new(), new(), new(), new(), new());

    private static RoundSummaryDto ToSummary(RoundMetricRow r) => new(r.Id, r.CourseId, r.CourseName,
        DateOnly.FromDateTime(r.Date), r.CourseTeeId, r.Tee, r.TotalScore, r.ScoreToPar,
        "Completed", r.HoleCount, r.HoleCount);

    private static RoundStatisticsDto BuildRoundStatistics(int roundId, List<Hole> holes)
    {
        var score = holes.Sum(h => h.Score); var par = holes.Sum(h => h.Par); var putts = holes.Sum(h => h.Putts);
        var count = holes.Count; var fairway = holes.Where(h => h.Par != 3 && h.FairwayHit.HasValue).ToList();
        return new(roundId, score, score - par, putts, count == 0 ? 0 : (double)putts / count,
            count == 0 ? 0 : 100.0 * holes.Count(h => h.GIR) / count,
            fairway.Count == 0 ? null : 100.0 * fairway.Count(h => h.FairwayHit == true) / fairway.Count,
            holes.Sum(h => h.Penalty), holes.Count(h => h.Score - h.Par <= -2), holes.Count(h => h.Score - h.Par == -1),
            holes.Count(h => h.Score - h.Par == 0), holes.Count(h => h.Score - h.Par == 1), holes.Count(h => h.Score - h.Par >= 2),
            ParStats(holes, 3), ParStats(holes, 4), ParStats(holes, 5));
    }

    private static ParTypeStatsDto ParStats(List<Hole> holes, int par) {
        var selected = holes.Where(h => h.Par == par).ToList();
        return selected.Count == 0 ? new(0, 0, 0) : new(selected.Count, selected.Average(h => h.Score), selected.Average(h => h.Score - h.Par));
    }

    private static List<MovingAveragePointDto> BuildMovingAverages(List<RoundMetricRow> rows)
    {
        var result = new List<MovingAveragePointDto>();
        for (var i = 0; i < rows.Count; i++)
        {
            double? Average(int window, Func<RoundMetricRow, double> value) => i + 1 < window ? null : rows.Skip(i + 1 - window).Take(window).Average(value);
            result.Add(new(DateOnly.FromDateTime(rows[i].Date), Average(5, r => (double)r.ScoreToPar / r.HoleCount),
                Average(10, r => (double)r.ScoreToPar / r.HoleCount), Average(5, r => (double)r.TotalPutts / r.HoleCount),
                Average(10, r => (double)r.TotalPutts / r.HoleCount)));
        }
        return result;
    }

    private sealed class RoundMetricRow
    {
        public int Id { get; init; } public int CourseId { get; init; } public string CourseName { get; init; } = "";
        public int? CourseTeeId { get; init; } public string Tee { get; init; } = ""; public DateTime Date { get; init; }
        public int HoleCount { get; init; } public int TotalScore { get; init; } public int TotalPar { get; init; }
        public int TotalPutts { get; init; } public int GirCount { get; init; } public int FairwayEligible { get; init; }
        public int FairwayHits { get; init; } public int ScoreToPar => TotalScore - TotalPar;
    }

    private sealed class ParMetricRow
    {
        public int RoundId { get; init; } public int Par { get; init; } public int Holes { get; init; }
        public int ScoreToPar { get; init; } public int GirCount { get; init; }
    }
}
