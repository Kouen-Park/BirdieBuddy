using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using BirdieBuddy.Services;
using Microsoft.EntityFrameworkCore;
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

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
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
