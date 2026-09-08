using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using BirdieBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace BirdieBuddy.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BIRDIEBUDDY_TEST_POSTGRES")))
            Skip = "Set BIRDIEBUDDY_TEST_POSTGRES to a disposable local PostgreSQL test database.";
    }
}

public class PostgresIntegrationTests
{
    [PostgresFact]
    [Trait("Category", "PostgreSQL")]
    public async Task MigrationsLifecycleOwnershipUniquenessAndConcurrentRollback()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BIRDIEBUDDY_TEST_POSTGRES"));
        // Never migrate a production database accidentally, and never delete an existing database.
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        Assert.StartsWith("birdiebuddy_test_", connection.Database);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260905150000_AddProductTelemetry");

        // Simulate the existing Render data that must survive the next deployment.
        var user = new User { Email = $"{Guid.NewGuid():N}@example.test", DisplayName = "Integration", CreatedAt = DateTime.UtcNow };
        var tee = new CourseTee { Name = "White", NineHoles = true, CourseHoles = Enumerable.Range(1, 9)
            .Select(n => new CourseHole { HoleNumber = n, Par = 4, Distance = 300 }).ToList() };
        var course = new Course { Name = "Integration course", Location = "NZ", CourseTees = new() { tee } };
        var historicalRound = new Round
        {
            User = user,
            Course = course,
            CourseTee = tee,
            Date = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            Status = RoundStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-4),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1),
            Holes = new() { new Hole { HoleNumber = 1, Par = 4, Score = 5, Putts = 2, GIR = false, FairwayHit = true } }
        };
        db.Rounds.Add(historicalRound);
        await db.SaveChangesAsync();
        Assert.Null(user.EmailVerifiedAt);

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var preservedUser = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
        Assert.Equal(preservedUser.CreatedAt, preservedUser.EmailVerifiedAt);
        var preservedRound = await db.Rounds.Include(round => round.Holes)
            .SingleAsync(round => round.Id == historicalRound.Id);
        Assert.Equal(course.Id, preservedRound.CourseId);
        Assert.Equal(tee.Id, preservedRound.CourseTeeId);
        Assert.Single(preservedRound.Holes);

        var service = new RoundService(db, new TestUser(user.Id));
        var (draft, error) = await service.StartAsync(new(course.Id, new(2026, 9, 3), tee.Id, null));
        Assert.Null(error);
        Assert.NotNull(draft);
        Assert.Null(await new RoundService(db, new TestUser(user.Id + 1)).GetByIdAsync(draft!.Id));
        var first = await service.UpsertHoleAsync(draft.Id, 1, new(4, 4, 2, true, true, 0));
        Assert.Null(first.Error);

        await using (var staleDb = new ApplicationDbContext(options))
        {
            var staleRound = await staleDb.Rounds.Include(r => r.Holes).SingleAsync(r => r.Id == draft.Id);
            Assert.Null((await service.UpsertHoleAsync(draft.Id, 1, new(4, 5, 2, false, false, 0))).Error);
            staleRound.Holes[0].Score = 9;
            staleRound.UpdatedAt = staleRound.UpdatedAt.AddSeconds(1);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());
        }
        await using (var check = new ApplicationDbContext(options))
        {
            Assert.Equal(5, (await check.Holes.SingleAsync(h => h.RoundId == draft.Id)).Score);
            check.Holes.Add(new Hole { RoundId = draft.Id, HoleNumber = 1, Par = 4, Score = 4, Putts = 2 });
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        }
        var staleResult = await service.UpsertHoleAsync(draft.Id, 1, new(4, 6, 2, false, false, 0, true, first.Hole));
        Assert.Equal(RoundService.ConflictMessage, staleResult.Error);
        foreach (var n in Enumerable.Range(2, 8))
            Assert.Null((await service.UpsertHoleAsync(draft.Id, n, new(4, 4, 2, true, true, 0))).Error);
        Assert.Equal("Completed", (await service.CompleteAsync(draft.Id)).Round!.Status);
        var stats = await new StatisticsService(db, new TestUser(user.Id)).GetOverviewStatisticsAsync(new(HoleCount: 9));
        Assert.Equal(1, stats.RoundsPlayed);
        Assert.Equal(37, stats.ByRoundLength![0].AverageScore);
    }

    private sealed record TestUser(int? Id) : ICurrentUser { public bool IsAuthenticated => true; }
}
