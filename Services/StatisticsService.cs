using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public class StatisticsService : IStatisticsService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public StatisticsService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<RoundStatisticsDto?> GetRoundStatisticsAsync(int roundId)
    {
        var round = await _context.Rounds
            .Where(r => r.Id == roundId && r.UserId == CurrentUserId)
            .Include(r => r.Holes)
            .FirstOrDefaultAsync();

        return round is null ? null : BuildRoundStatistics(roundId, round.Holes);
    }

    public async Task<OverviewStatisticsDto> GetOverviewStatisticsAsync(StatisticsQueryDto? query = null)
    {
        query ??= new StatisticsQueryDto();
        var roundQuery = _context.Rounds
            .AsNoTracking()
            .Where(r => r.UserId == CurrentUserId && r.Status == RoundStatus.Completed && r.Holes.Any());
        if (query.CourseId.HasValue) roundQuery = roundQuery.Where(r => r.CourseId == query.CourseId.Value);
        if (query.CourseTeeId.HasValue) roundQuery = roundQuery.Where(r => r.CourseTeeId == query.CourseTeeId.Value);
        if (query.From.HasValue)
        {
            var from = DateTime.SpecifyKind(query.From.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            roundQuery = roundQuery.Where(r => r.Date >= from);
        }
        if (query.To.HasValue)
        {
            var to = DateTime.SpecifyKind(query.To.Value.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
            roundQuery = roundQuery.Where(r => r.Date <= to);
        }
        if (query.HoleCount.HasValue) roundQuery = roundQuery.Where(r => r.Holes.Count == query.HoleCount.Value);

        var rounds = await roundQuery
            .Include(r => r.Holes)
            .Include(r => r.Course)
            .Include(r => r.CourseTee)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync();

        if (rounds.Count == 0)
        {
            return new OverviewStatisticsDto(0, 0, 0, 0, 0, null,
                new List<RoundSummaryDto>(), new List<TrendPointDto>(),
                new List<TrendPointDto>(), new List<TrendPointDto>(), null, null,
                BuildInsights(new()), 0, new(), new(), new(), new(), new());
        }

        var roundStats = rounds
            .Select(r => (Round: r, Stats: BuildRoundStatistics(r.Id, r.Holes)))
            .ToList();

        double avgScore = roundStats.Average(x => x.Stats.TotalScore);
        int bestScore = roundStats.Min(x => x.Stats.TotalScore);
        double avgPutts = roundStats.Average(x => x.Stats.TotalPutts);
        var allHoles = rounds.SelectMany(r => r.Holes).ToList();
        double avgGir = 100.0 * allHoles.Count(h => h.GIR) / allHoles.Count;
        var fairwayHoles = allHoles.Where(h => h.Par != 3 && h.FairwayHit.HasValue).ToList();
        double? avgFairway = fairwayHoles.Count > 0
            ? 100.0 * fairwayHoles.Count(h => h.FairwayHit == true) / fairwayHoles.Count
            : null;

        var recent = roundStats
            .Take(5)
            .Select(x => new RoundSummaryDto(
                x.Round.Id, x.Round.CourseId, x.Round.Course?.Name ?? "", DateOnly.FromDateTime(x.Round.Date),
                x.Round.CourseTeeId, x.Round.CourseTee?.Name ?? x.Round.LegacyTee ?? "Unknown",
                x.Stats.TotalScore, x.Stats.ScoreToPar, "Completed", x.Round.Holes.Count, x.Round.Holes.Count))
            .ToList();

        var chronological = roundStats.OrderBy(x => x.Round.Date).ThenBy(x => x.Round.Id).ToList();
        var scoreTrend = chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date), x.Stats.TotalScore)).ToList();
        var girTrend = chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date), x.Stats.GirPercentage)).ToList();
        var puttsTrend = chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date), x.Stats.TotalPutts)).ToList();

        var groups = roundStats.GroupBy(x => x.Round.Holes.Count).OrderBy(g => g.Key)
            .Select(g => new RoundLengthStatisticsDto(g.Key, g.Count(), g.Average(x => x.Stats.TotalScore),
                g.Min(x => x.Stats.TotalScore), g.Average(x => x.Stats.ScoreToPar),
                g.Count() >= 5 ? g.Take(5).Average(x => x.Stats.ScoreToPar) : null,
                g.Count() >= 10 ? g.Take(10).Average(x => x.Stats.ScoreToPar) : null)).ToList();
        // Legacy totals stay available for older clients, but never publish mixed-length moving averages.
        double? recentFive = groups.Count == 1 ? groups[0].RecentFiveScoreToPar : null;
        double? recentTen = groups.Count == 1 ? groups[0].RecentTenScoreToPar : null;
        var insights = BuildInsights(roundStats);

        return new OverviewStatisticsDto(
            roundStats.Count, avgScore, bestScore, avgPutts, avgGir, avgFairway,
            recent, scoreTrend, girTrend, puttsTrend, recentFive, recentTen, insights,
            allHoles.Average(h => h.Putts), groups,
            chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date),
                (double)x.Stats.ScoreToPar / x.Round.Holes.Count)).ToList(),
            chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date),
                x.Stats.AveragePuttsPerHole)).ToList(),
            BuildMovingAverages(chronological),
            chronological.SelectMany(x => x.Round.Holes.GroupBy(h => h.Par).Select(g => new ParTypeTrendPointDto(
                DateOnly.FromDateTime(x.Round.Date), g.Key, g.Average(h => h.Score - h.Par),
                100.0 * g.Count(h => h.GIR) / g.Count()))).ToList());
    }

    private static RoundStatisticsDto BuildRoundStatistics(int roundId, List<Hole> holes)
    {
        int totalScore = holes.Sum(h => h.Score);
        int totalPar = holes.Sum(h => h.Par);
        int totalPutts = holes.Sum(h => h.Putts);
        int totalPenalties = holes.Sum(h => h.Penalty);

        int holeCount = holes.Count;
        double avgPutts = holeCount > 0 ? (double)totalPutts / holeCount : 0;

        // Rates use recorded holes; fairways explicitly exclude historical par-3 values.
        int girHoles = holes.Count(h => h.GIR);
        double girPct = holeCount > 0 ? (double)girHoles / holeCount * 100 : 0;

        // Fairway% only ever considers holes where FairwayHit is applicable (i.e. not par 3s).
        var fairwayEligible = holes.Where(h => h.Par != 3 && h.FairwayHit.HasValue).ToList();
        double? fairwayPct = fairwayEligible.Count > 0
            ? (double)fairwayEligible.Count(h => h.FairwayHit == true) / fairwayEligible.Count * 100
            : null;

        int eagles = holes.Count(h => h.Score - h.Par <= -2);
        int birdies = holes.Count(h => h.Score - h.Par == -1);
        int pars = holes.Count(h => h.Score - h.Par == 0);
        int bogeys = holes.Count(h => h.Score - h.Par == 1);
        int doubleOrWorse = holes.Count(h => h.Score - h.Par >= 2);

        var par3 = BuildParTypeStats(holes.Where(h => h.Par == 3).ToList());
        var par4 = BuildParTypeStats(holes.Where(h => h.Par == 4).ToList());
        var par5 = BuildParTypeStats(holes.Where(h => h.Par == 5).ToList());

        return new RoundStatisticsDto(
            roundId, totalScore, totalScore - totalPar, totalPutts, avgPutts,
            girPct, fairwayPct, totalPenalties, eagles, birdies, pars, bogeys, doubleOrWorse,
            par3, par4, par5);
    }

    private static ParTypeStatsDto BuildParTypeStats(List<Hole> holes)
    {
        if (holes.Count == 0) return new ParTypeStatsDto(0, 0, 0);

        return new ParTypeStatsDto(
            holes.Count,
            holes.Average(h => h.Score),
            holes.Average(h => h.Score - h.Par));
    }

    private static List<PerformanceInsightDto> BuildInsights(
        List<(Round Round, RoundStatisticsDto Stats)> rounds)
        => PracticeInsights.Build(rounds.Select(x => x.Round));

    private static List<MovingAveragePointDto> BuildMovingAverages(
        List<(Round Round, RoundStatisticsDto Stats)> chronological)
    {
        var result = new List<MovingAveragePointDto>();
        for (var i = 0; i < chronological.Count; i++)
        {
            double? Average(int window, Func<(Round Round, RoundStatisticsDto Stats), double> value) => i + 1 < window
                ? null : chronological.Skip(i + 1 - window).Take(window).Average(value);
            result.Add(new MovingAveragePointDto(DateOnly.FromDateTime(chronological[i].Round.Date),
                Average(5, x => (double)x.Stats.ScoreToPar / Math.Max(1, x.Round.Holes.Count)),
                Average(10, x => (double)x.Stats.ScoreToPar / Math.Max(1, x.Round.Holes.Count)),
                Average(5, x => x.Stats.AveragePuttsPerHole), Average(10, x => x.Stats.AveragePuttsPerHole)));
        }
        return result;
    }
}
