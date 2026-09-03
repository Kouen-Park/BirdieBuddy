using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using BirdieBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BirdieBuddy.Tests;

public class StatisticsServiceTests
{
    [Fact]
    public async Task MixedLengthsAreGroupedAndRatesAreHoleWeighted()
    {
        await using var db = Database();
        AddRound(db, 9, score: 5, putts: 1, gir: true);
        AddRound(db, 18, score: 6, putts: 2, gir: false);
        await db.SaveChangesAsync();
        var result = await new StatisticsService(db, new TestUser(7)).GetOverviewStatisticsAsync();
        Assert.Equal(2, result.ByRoundLength!.Count);
        Assert.Equal(45, result.ByRoundLength[0].AverageScore);
        Assert.Equal(108, result.ByRoundLength[1].AverageScore);
        Assert.Equal(5.0 / 3, result.AveragePuttsPerHole, 6);
        Assert.Equal(100.0 / 3, result.AverageGirPercentage, 6);
        Assert.Null(result.RecentFiveScoreToPar);
        Assert.Equal(new double[] { 1, 2 }, result.ScoreToParPerHoleTrend!.Select(p => p.Value));
        Assert.Equal(new[] { 18, 9 }, result.RecentRounds.Select(r => r.HolesPlayed));
    }

    [Fact]
    public async Task RecentAveragesRequireFullSameLengthWindowAndUseIdAsDateTieBreaker()
    {
        await using var db = Database();
        for (var i = 0; i < 6; i++) AddRound(db, 9, score: i + 4);
        AddRound(db, 18, score: 20);
        await db.SaveChangesAsync();
        var result = await new StatisticsService(db, new TestUser(7)).GetOverviewStatisticsAsync();
        Assert.Equal(27, result.ByRoundLength![0].RecentFiveScoreToPar);
        Assert.Null(result.ByRoundLength[0].RecentTenScoreToPar);
        Assert.Null(result.ByRoundLength[1].RecentFiveScoreToPar);
    }

    [Fact]
    public async Task OverviewExcludesOtherOwnersEmptyAndUnfinishedRounds()
    {
        await using var db = Database();
        AddRound(db, 9);
        AddRound(db, 18).UserId = 8;
        AddRound(db, 9).Status = RoundStatus.Draft;
        AddRound(db, 9).Status = RoundStatus.Abandoned;
        AddRound(db, 0);
        await db.SaveChangesAsync();
        var service = new StatisticsService(db, new TestUser(7));
        Assert.Equal(1, (await service.GetOverviewStatisticsAsync()).RoundsPlayed);
        Assert.Null(await service.GetRoundStatisticsAsync(2));
    }

    [Fact]
    public async Task ParThreeFairwaysAreExcludedEvenInHistoricalRecords()
    {
        await using var db = Database();
        var round = AddRound(db, 9);
        round.Holes[0].Par = 3;
        round.Holes[0].FairwayHit = true;
        foreach (var hole in round.Holes.Skip(1)) hole.FairwayHit = false;
        await db.SaveChangesAsync();
        var service = new StatisticsService(db, new TestUser(7));
        Assert.Equal(0d, (await service.GetRoundStatisticsAsync(round.Id))!.FairwayPercentage);
        Assert.Equal(0d, (await service.GetOverviewStatisticsAsync()).AverageFairwayPercentage);
    }

    [Fact]
    public async Task FiltersCombineAndEndDateIncludesWholeDay()
    {
        await using var db = Database();
        var selected = AddRound(db, 9);
        selected.CourseTee = new CourseTee { Id = 23, Name = "White", Course = selected.Course };
        db.CourseTees.Add(selected.CourseTee);
        selected.CourseTeeId = 23;
        selected.Date = new DateTime(2026, 9, 3, 23, 59, 59, DateTimeKind.Utc);
        AddRound(db, 9);
        await db.SaveChangesAsync();
        var service = new StatisticsService(db, new TestUser(7));
        var result = await service.GetOverviewStatisticsAsync(new(selected.CourseId, new(2026, 9, 3), new(2026, 9, 3), 9, 23));
        Assert.Single(result.RecentRounds);
        Assert.Equal(selected.Id, result.RecentRounds[0].Id);
        Assert.Equal(0, (await service.GetOverviewStatisticsAsync(new(HoleCount: 18))).RoundsPlayed);
        await service.GetOverviewStatisticsAsync(new(To: DateOnly.MaxValue));
    }

    [Fact]
    public async Task EmptyOverviewExplainsInsightMinimum()
    {
        await using var db = Database();
        var result = await new StatisticsService(db, new TestUser(7)).GetOverviewStatisticsAsync();
        Assert.Empty(result.ByRoundLength!);
        Assert.Empty(result.PuttsPerHoleTrend!);
        Assert.Equal("more-data", Assert.Single(result.Insights!).Code);
    }

    private static ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Round AddRound(ApplicationDbContext db, int count, int score = 4, int putts = 2, bool gir = false)
    {
        var round = new Round { UserId = 7, Course = new Course { Name = "Test Club", Location = "NZ" },
            Date = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc),
            Holes = Enumerable.Range(1, count).Select(n => new Hole {
                HoleNumber = n, Par = 4, Score = score, Putts = putts, GIR = gir
            }).ToList() };
        db.Rounds.Add(round);
        return round;
    }

    private sealed record TestUser(int? Id) : ICurrentUser { public bool IsAuthenticated => true; }
}
