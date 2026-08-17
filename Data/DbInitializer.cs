using BirdieBuddy.Models;

namespace BirdieBuddy.Data;

// Seeds two example courses and a handful of sample rounds on first run,
// purely so the dashboard/charts have something to show immediately.
// This is demo/test data only - it is not distinguished from "real" data
// at the schema level, but nothing here ever re-runs once a Course exists.
public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        if (context.Courses.Any()) return; // already seeded (or real data exists)

        var course1 = new Course { Name = "Fairway Ridge Golf Club", Location = "Hamilton, NZ" };
        var course2 = new Course { Name = "Coastal Dunes Links", Location = "Tauranga, NZ" };

        AddHoles(course1, new (int Par, int Distance)[]
        {
            (4,380), (5,495), (3,165), (4,350), (4,410), (3,180), (5,510), (4,365), (4,395),
            (4,375), (4,340), (5,480), (3,155), (4,400), (4,385), (3,170), (5,520), (4,405)
        });

        AddHoles(course2, new (int Par, int Distance)[]
        {
            (4,370), (3,175), (5,505), (4,355), (4,400), (3,145), (4,390), (5,515), (4,360),
            (3,160), (4,410), (4,340), (5,490), (3,185), (4,375), (4,395), (3,150), (5,530)
        });

        context.Courses.AddRange(course1, course2);
        context.SaveChanges();

        // skillLevel shifts average score: negative = plays better than par, positive = worse.
        SeedRound(context, course1, new DateTime(2026, 5, 3), "White", seed: 1, skillLevel: 1);
        SeedRound(context, course1, new DateTime(2026, 5, 18), "White", seed: 2, skillLevel: 0);
        SeedRound(context, course1, new DateTime(2026, 6, 8), "Blue", seed: 3, skillLevel: 0);
        SeedRound(context, course2, new DateTime(2026, 6, 22), "White", seed: 4, skillLevel: -1);
        SeedRound(context, course1, new DateTime(2026, 7, 12), "White", seed: 5, skillLevel: -1);
        context.SaveChanges();
    }

    private static void AddHoles(Course course, (int Par, int Distance)[] holes)
    {
        for (int i = 0; i < holes.Length; i++)
        {
            course.CourseHoles.Add(new CourseHole
            {
                HoleNumber = i + 1,
                Par = holes[i].Par,
                Distance = holes[i].Distance
            });
        }
    }

    private static void SeedRound(ApplicationDbContext context, Course course, DateTime date, string tee, int seed, int skillLevel)
    {
        var rnd = new Random(seed);
        var round = new Round { CourseId = course.Id, Date = date, Tee = tee };

        foreach (var ch in course.CourseHoles.OrderBy(c => c.HoleNumber))
        {
            int offset = skillLevel + rnd.Next(0, 3) - (rnd.NextDouble() < 0.15 ? 1 : 0);
            int score = Math.Max(1, ch.Par + offset);
            int putts = Math.Clamp(2 + (score - ch.Par > 0 ? rnd.Next(0, 2) : 0), 1, 4);
            bool gir = score <= ch.Par && rnd.NextDouble() < (skillLevel <= 0 ? 0.55 : 0.35);
            bool? fairway = ch.Par == 3 ? null : rnd.NextDouble() < (skillLevel <= 0 ? 0.65 : 0.45);
            int penalty = rnd.NextDouble() < 0.1 ? 1 : 0;

            round.Holes.Add(new Hole
            {
                HoleNumber = ch.HoleNumber,
                Par = ch.Par,
                Score = score,
                Putts = putts,
                GIR = gir,
                FairwayHit = fairway,
                Penalty = penalty
            });
        }

        context.Rounds.Add(round);
    }
}
