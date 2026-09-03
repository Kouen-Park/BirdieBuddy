using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using BirdieBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace BirdieBuddy.Tests;

public sealed class RoundServiceTests
{
    [Fact]
    public void LiveRoundMigration_IsDiscoverable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
        using var db = new ApplicationDbContext(options);

        Assert.Contains("20260903120000_AddLiveRoundLifecycle", db.Database.GetMigrations());
    }

    [Fact]
    public async Task LiveHoleUpsert_IsIdempotentByHoleNumber()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db);
        var service = new RoundService(db, new TestUser(7));
        var (draft, startError) = await service.StartAsync(new RoundStartDto(course.Id, DateOnly.FromDateTime(DateTime.Today), tee.Id, null));

        Assert.Null(startError);
        var first = await service.UpsertHoleAsync(draft!.Id, 1, new HoleUpsertDto(null, 5, 2, false, false, 0));
        var second = await service.UpsertHoleAsync(draft.Id, 1, new HoleUpsertDto(null, 4, 1, true, true, 0));

        Assert.Null(first.Error);
        Assert.Null(second.Error);
        Assert.Equal(1, await db.Holes.CountAsync());
        Assert.Equal(4, (await db.Holes.SingleAsync()).Score);
    }

    [Fact]
    public async Task RoundOwnership_PreventsAnotherUserFromReadingDraft()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db);
        var owner = new RoundService(db, new TestUser(7));
        var (draft, _) = await owner.StartAsync(new RoundStartDto(course.Id, DateOnly.FromDateTime(DateTime.Today), tee.Id, null));

        var otherUser = new RoundService(db, new TestUser(8));

        Assert.Null(await otherUser.GetByIdAsync(draft!.Id));
        Assert.False(await otherUser.AbandonAsync(draft.Id));
    }

    [Fact]
    public async Task DraftCannotCompleteUntilEveryExpectedHoleIsSaved()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db, nineHoles: true);
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new RoundStartDto(course.Id, DateOnly.FromDateTime(DateTime.Today), tee.Id, null));
        await service.UpsertHoleAsync(draft!.Id, 1, new HoleUpsertDto(null, 4, 2, true, true, 0));

        var (completed, error) = await service.CompleteAsync(draft.Id);

        Assert.Null(completed);
        Assert.Contains("all 9 holes", error);
    }

    [Fact]
    public async Task BackNineUsesActualHoleNumbersAndRejectsFrontNine()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db, nineHoles: true);
        foreach (var hole in tee.CourseHoles) hole.HoleNumber += 9;
        await db.SaveChangesAsync();
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new(course.Id, new DateOnly(2026, 9, 3), tee.Id, null));
        Assert.Equal(10, draft!.CurrentHole);
        Assert.Equal(Enumerable.Range(10, 9), draft.HoleNumbers);
        Assert.NotNull((await service.UpsertHoleAsync(draft.Id, 1, new(4, 4, 2, true, true, 0))).Error);
        foreach (var number in Enumerable.Range(10, 9))
            Assert.Null((await service.UpsertHoleAsync(draft.Id, number, new(4, 4, 2, true, true, 0))).Error);
        Assert.Equal("Completed", (await service.CompleteAsync(draft.Id)).Round!.Status);
    }

    [Fact]
    public async Task CompletionChecksHoleIdentityNotJustCount()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db, nineHoles: true);
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new(course.Id, new DateOnly(2026, 9, 3), tee.Id, null));
        db.Holes.AddRange(Enumerable.Range(10, 9).Select(number => new Hole
            { RoundId = draft!.Id, HoleNumber = number, Par = 4, Score = 4, Putts = 2 }));
        await db.SaveChangesAsync();
        Assert.NotNull((await service.CompleteAsync(draft!.Id)).Error);
    }

    [Fact]
    public async Task StaleOfflineEditConflictsButLostResponseCanBeRetried()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db);
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new(course.Id, new DateOnly(2026, 9, 3), tee.Id, null));
        var initial = new HoleUpsertDto(4, 5, 2, false, false, 0, true, null);
        var first = await service.UpsertHoleAsync(draft!.Id, 1, initial);
        Assert.Null((await service.UpsertHoleAsync(draft.Id, 1, initial)).Error);
        var newer = await service.UpsertHoleAsync(draft.Id, 1, new(4, 4, 1, true, true, 0, true, first.Hole));
        Assert.Null(newer.Error);
        var stale = await service.UpsertHoleAsync(draft.Id, 1, new(4, 6, 2, false, false, 0, true, first.Hole));
        Assert.Equal(RoundService.ConflictMessage, stale.Error);
        Assert.Equal(4, (await db.Holes.SingleAsync()).Score);
    }

    [Fact]
    public async Task AbandonedRoundCannotBeMutatedThroughLegacyRoutes()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db);
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new(course.Id, new DateOnly(2026, 9, 3), tee.Id, null));
        var first = await service.UpsertHoleAsync(draft!.Id, 1, new(4, 4, 2, true, true, 0));
        await service.AbandonAsync(draft.Id);
        Assert.NotNull((await service.AddHoleAsync(draft.Id, new(2, 4, 4, 2, true, true, 0))).Error);
        Assert.False((await service.UpdateHoleAsync(draft.Id, first.Hole!.Id, new(5, 2, false, false, 0))).Success);
        Assert.False(await service.UpdateAsync(draft.Id, new(new DateOnly(2026, 9, 4), tee.Id, null)));
    }

    [Fact]
    public async Task CompletedHoleEditStillRejectsFairwayOnParThree()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db, nineHoles: true);
        tee.CourseHoles[0].Par = 3;
        await db.SaveChangesAsync();
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new(course.Id, new DateOnly(2026, 9, 3), tee.Id, null));
        foreach (var number in Enumerable.Range(1, 9))
            await service.UpsertHoleAsync(draft!.Id, number, new(null, 4, 2, true, null, 0));
        await service.CompleteAsync(draft!.Id);
        var first = await db.Holes.SingleAsync(h => h.HoleNumber == 1);
        Assert.False((await service.UpdateHoleAsync(draft.Id, first.Id, new(4, 2, true, true, 0))).Success);
        Assert.False((await service.UpdateHoleAsync(draft.Id, first.Id, new(2, 3, true, null, 0))).Success);
    }

    [Fact]
    public async Task RoundTimestampDetectsConcurrentWrites()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db);
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new(course.Id, new DateOnly(2026, 9, 3), tee.Id, null));
        // Independent context, same provider/database, mimics a stale request.
        var options = db.GetService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>();
        await using var other = new ApplicationDbContext((DbContextOptions<ApplicationDbContext>)options);
        var stale = await other.Rounds.SingleAsync(r => r.Id == draft!.Id);
        await service.AbandonAsync(draft!.Id);
        stale.UpdatedAt = stale.UpdatedAt.AddDays(1);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
    }

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CompletedHoleEditsRejectStaleSnapshotsAndOtherOwners()
    {
        await using var db = CreateDatabase();
        var (course, tee) = SeedCourse(db, nineHoles: true);
        var service = new RoundService(db, new TestUser(7));
        var (draft, _) = await service.StartAsync(new(course.Id, new(2026, 9, 3), tee.Id, null));
        foreach (var n in Enumerable.Range(1, 9))
            await service.UpsertHoleAsync(draft!.Id, n, new(4, 4, 2, true, true, 0));
        var completed = (await service.CompleteAsync(draft!.Id)).Round!;
        var original = completed.Holes[0];
        var change = new HoleUpdateDto(5, 2, false, false, 0, true, original);
        Assert.False((await new RoundService(db, new TestUser(8)).UpdateHoleAsync(draft.Id, original.Id, change)).Success);
        Assert.True((await service.UpdateHoleAsync(draft.Id, original.Id, change)).Success);
        Assert.True((await service.UpdateHoleAsync(draft.Id, original.Id, change)).Success);
        Assert.Equal(RoundService.ConflictMessage,
            (await service.UpdateHoleAsync(draft.Id, original.Id, change with { Score = 6 })).Error);
        Assert.Equal(5, (await service.GetByIdAsync(draft.Id))!.Holes[0].Score);
    }

    private static (Course Course, CourseTee Tee) SeedCourse(ApplicationDbContext db, bool nineHoles = false)
    {
        var tee = new CourseTee
        {
            Name = "White", CourseType = "CUSTOM", Gender = "", NineHoles = nineHoles,
            CourseHoles = Enumerable.Range(1, nineHoles ? 9 : 18)
                .Select(number => new CourseHole { HoleNumber = number, Par = 4, Distance = 350 }).ToList()
        };
        var course = new Course { Name = "Test Club", Location = "NZ", CourseTees = new() { tee } };
        db.Courses.Add(course);
        db.SaveChanges();
        return (course, tee);
    }

    private sealed record TestUser(int UserId) : ICurrentUser
    {
        public int? Id => UserId;
        public bool IsAuthenticated => true;
    }
}
