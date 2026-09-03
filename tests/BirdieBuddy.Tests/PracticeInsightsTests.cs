using BirdieBuddy.Models;
using BirdieBuddy.Services;
using Xunit;

namespace BirdieBuddy.Tests;

public class PracticeInsightsTests
{
    [Fact]
    public void PartialAndUnfinishedRoundsDoNotUnlockPriorities()
    {
        var rounds = new[] { Round(1), Round(2), Round(3, count: 1), Round(4) };
        rounds[3].Status = RoundStatus.Draft;
        var insight = Assert.Single(PracticeInsights.Build(rounds));
        Assert.Equal("more-data", insight.Code);
        Assert.Contains("2 of 3", insight.Evidence);
        Assert.Null(insight.Drill);
    }

    [Fact]
    public void SixRoundsCompareNonOverlappingWindowsWithEvidenceLinks()
    {
        var rounds = Enumerable.Range(1, 6).Select(i => Round(i, gir: i <= 3)).ToList();
        var insight = Assert.Single(PracticeInsights.Build(rounds), i => i.Code == "par-4-gir-decline");
        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, insight.EvidenceRoundIds);
        Assert.Contains("0/27", insight.Evidence);
        Assert.Contains("27/27", insight.Evidence);
        Assert.NotNull(insight.Drill);
        Assert.Equal(3, insight.Drill!.Steps.Count);
        Assert.DoesNotContain(PracticeInsights.Build(rounds), i => i.Code == "steady");
    }

    [Fact]
    public void ParTypeSampleMinimumBlocksSmallSampleDeclines()
    {
        var rounds = Enumerable.Range(1, 6).Select(i => Round(i, gir: i <= 3)).ToList();
        foreach (var round in rounds)
            foreach (var hole in round.Holes.Take(3)) hole.Par = 3;
        Assert.DoesNotContain(PracticeInsights.Build(rounds), i => i.Code == "par-3-gir-decline");
        Assert.Contains(PracticeInsights.Build(rounds), i => i.Code == "par-4-gir-decline");
    }

    [Fact]
    public void ImprovementsAndSmallChangesAreNotCalledDeclines()
    {
        var rounds = Enumerable.Range(1, 6).Select(i => Round(i, gir: true)).ToList();
        rounds[5].Holes[0].GIR = false;
        Assert.DoesNotContain(PracticeInsights.Build(rounds), i => i.Code.EndsWith("gir-decline"));
        foreach (var round in rounds.Take(3)) foreach (var hole in round.Holes) hole.GIR = false;
        Assert.DoesNotContain(PracticeInsights.Build(rounds), i => i.Code.EndsWith("gir-decline"));
    }

    [Fact]
    public void ParThreeAndMissingFairwaysAreExcluded()
    {
        var rounds = Enumerable.Range(1, 3).Select(i => Round(i)).ToList();
        foreach (var round in rounds)
        {
            foreach (var hole in round.Holes.Take(4)) { hole.Par = 3; hole.FairwayHit = false; }
            foreach (var hole in round.Holes.Skip(4)) hole.FairwayHit = null;
        }
        Assert.DoesNotContain(PracticeInsights.Build(rounds), i => i.Code == "fairway");
    }

    [Fact]
    public void MixedLengthsUsePerHolePuttingAndPenaltyRates()
    {
        var rounds = new[] { Round(1), Round(2), Round(3, count: 18) };
        foreach (var hole in rounds[2].Holes) hole.Putts = 3;
        rounds[0].Holes[0].Penalty = 3;
        var insights = PracticeInsights.Build(rounds);
        Assert.Contains(insights, i => i.Code == "putting" && i.Evidence.Contains("2.00"));
        Assert.Contains(insights, i => i.Code == "penalties" && i.Evidence.Contains("1.50"));
        Assert.Contains(insights, i => i.Code == "trend-baseline");
        Assert.All(insights.Where(i => i.Severity != "info"), i => Assert.NotNull(i.Drill));
    }

    private static Round Round(int id, int count = 9, bool gir = true) => new() {
        Id = id, Date = new DateTime(2026, 9, id, 0, 0, 0, DateTimeKind.Utc),
        Holes = Enumerable.Range(1, count).Select(n => new Hole {
            HoleNumber = n, Par = 4, Score = 4, Putts = 1, GIR = gir, FairwayHit = true
        }).ToList()
    };
}
