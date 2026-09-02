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
            .Where(r => r.UserId == CurrentUserId && r.Status == RoundStatus.Completed);
        if (query.CourseId.HasValue) roundQuery = roundQuery.Where(r => r.CourseId == query.CourseId.Value);
        if (query.From.HasValue)
        {
            var from = DateTime.SpecifyKind(query.From.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            roundQuery = roundQuery.Where(r => r.Date >= from);
        }
        if (query.To.HasValue)
        {
            var to = DateTime.SpecifyKind(query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            roundQuery = roundQuery.Where(r => r.Date < to);
        }
        if (query.HoleCount.HasValue) roundQuery = roundQuery.Where(r => r.Holes.Count == query.HoleCount.Value);

        var rounds = await roundQuery
            .Include(r => r.Holes)
            .Include(r => r.Course)
            .Include(r => r.CourseTee)
            .OrderByDescending(r => r.Date)
            .ToListAsync();

        if (rounds.Count == 0)
        {
            return new OverviewStatisticsDto(0, 0, 0, 0, 0, null,
                new List<RoundSummaryDto>(), new List<TrendPointDto>(),
                new List<TrendPointDto>(), new List<TrendPointDto>(), null, null,
                new List<PerformanceInsightDto>());
        }

        var roundStats = rounds
            .Select(r => (Round: r, Stats: BuildRoundStatistics(r.Id, r.Holes)))
            .ToList();

        double avgScore = roundStats.Average(x => x.Stats.TotalScore);
        int bestScore = roundStats.Min(x => x.Stats.TotalScore);
        double avgPutts = roundStats.Average(x => x.Stats.TotalPutts);
        double avgGir = roundStats.Average(x => x.Stats.GirPercentage);

        var fairwayEligibleRounds = roundStats.Where(x => x.Stats.FairwayPercentage.HasValue).ToList();
        double? avgFairway = fairwayEligibleRounds.Count > 0
            ? fairwayEligibleRounds.Average(x => x.Stats.FairwayPercentage!.Value)
            : null;

        var recent = roundStats
            .Take(5)
            .Select(x => new RoundSummaryDto(
                x.Round.Id, x.Round.CourseId, x.Round.Course?.Name ?? "", DateOnly.FromDateTime(x.Round.Date),
                x.Round.CourseTeeId, x.Round.CourseTee?.Name ?? x.Round.LegacyTee ?? "Unknown",
                x.Stats.TotalScore, x.Stats.ScoreToPar))
            .ToList();

        var chronological = roundStats.OrderBy(x => x.Round.Date).ToList();
        var scoreTrend = chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date), x.Stats.TotalScore)).ToList();
        var girTrend = chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date), x.Stats.GirPercentage)).ToList();
        var puttsTrend = chronological.Select(x => new TrendPointDto(x.Round.Id, DateOnly.FromDateTime(x.Round.Date), x.Stats.TotalPutts)).ToList();

        var recentFive = roundStats.Take(5).Average(x => x.Stats.ScoreToPar);
        var recentTen = roundStats.Take(10).Average(x => x.Stats.ScoreToPar);
        var insights = BuildInsights(roundStats);

        return new OverviewStatisticsDto(
            roundStats.Count, avgScore, bestScore, avgPutts, avgGir, avgFairway,
            recent, scoreTrend, girTrend, puttsTrend, recentFive, recentTen, insights);
    }

    private static RoundStatisticsDto BuildRoundStatistics(int roundId, List<Hole> holes)
    {
        int totalScore = holes.Sum(h => h.Score);
        int totalPar = holes.Sum(h => h.Par);
        int totalPutts = holes.Sum(h => h.Putts);
        int totalPenalties = holes.Sum(h => h.Penalty);

        int holeCount = holes.Count;
        double avgPutts = holeCount > 0 ? (double)totalPutts / holeCount : 0;

        // GIR% is measured against the full 18-hole round, per spec, not just holes recorded so far.
        int girHoles = holes.Count(h => h.GIR);
        double girPct = holeCount > 0 ? (double)girHoles / holeCount * 100 : 0;

        // Fairway% only ever considers holes where FairwayHit is applicable (i.e. not par 3s).
        var fairwayEligible = holes.Where(h => h.FairwayHit.HasValue).ToList();
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
    {
        if (rounds.Count < 3)
            return new List<PerformanceInsightDto>
            {
                new("more-data", "Build your baseline",
                    $"{rounds.Count} of 3 completed rounds recorded.",
                    $"Complete {3 - rounds.Count} more round{(3 - rounds.Count == 1 ? "" : "s")} to unlock practice insights.", "info")
            };

        var recent = rounds.Take(5).ToList();
        var insights = new List<PerformanceInsightDto>();
        var avgPuttsPerHole = recent.Sum(x => x.Stats.TotalPutts) /
            (double)Math.Max(1, recent.Sum(x => x.Round.Holes.Count));
        if (avgPuttsPerHole >= 2)
            insights.Add(new("putting", "Make putting the next focus",
                $"Your last {recent.Count} rounds average {avgPuttsPerHole:F2} putts per hole.",
                "Spend one practice block on pace control from 6–12 metres, then finish with short putts.", "high"));

        var avgGir = recent.Average(x => x.Stats.GirPercentage);
        if (avgGir < 40)
            insights.Add(new("approach", "Create more birdie chances",
                $"Greens in regulation are {avgGir:F0}% across your last {recent.Count} rounds.",
                "Track one stock approach distance and practise hitting the centre of the green from there.", "medium"));

        var penalties = recent.Sum(x => x.Stats.TotalPenalties);
        if (penalties > recent.Count)
            insights.Add(new("penalties", "Protect the scorecard",
                $"You recorded {penalties} penalty strokes in your last {recent.Count} rounds.",
                "Choose a conservative target on trouble holes and commit to a club that keeps the ball in play.", "high"));

        if (insights.Count == 0)
            insights.Add(new("steady", "Your fundamentals are balanced",
                $"No single tracked metric is outside the coaching thresholds over your last {recent.Count} rounds.",
                "Keep recording rounds and work on the weakest par type shown in Statistics.", "info"));
        return insights;
    }
}
