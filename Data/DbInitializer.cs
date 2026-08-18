using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Models;

namespace BirdieBuddy.Data;

// Seeds demo data on first run and updates the original demo courses
// when the database already contains the old seed data.
public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        context.Database.EnsureCreated();

        var fairway = context.Courses
            .Include(c => c.CourseHoles)
            .FirstOrDefault(c => c.Name == "Fairway Ridge Golf Club");

        var coastal = context.Courses
            .Include(c => c.CourseHoles)
            .FirstOrDefault(c => c.Name == "Coastal Dunes Links");

        // Update the existing demo data.
        if (fairway is not null && coastal is not null)
        {
            // Move rounds from Coastal Dunes to Whitford Park.
            var coastalRounds = context.Rounds
                .Where(r => r.CourseId == coastal.Id)
                .ToList();

            foreach (var round in coastalRounds)
            {
                round.CourseId = fairway.Id;
            }

            // Update Fairway Ridge to Whitford Park.
            fairway.Name = "Whitford Park Golf Club";
            fairway.Location = "Whitford, Auckland, NZ";

            // Replace the old hole data with Whitford Park data.
            fairway.CourseHoles.Clear();

            AddHoles(
                fairway,
                new (int Par, int Distance)[]
                {
                    (4, 270),
                    (4, 323),
                    (3, 107),
                    (4, 327),
                    (4, 347),
                    (5, 410),
                    (4, 326),
                    (4, 370),
                    (3, 180),
                    (4, 297),
                    (3, 140),
                    (5, 428),
                    (5, 460),
                    (4, 310),
                    (4, 288),
                    (4, 293),
                    (3, 117),
                    (4, 354)
                });

            // Delete the old Coastal Dunes course.
            context.Courses.Remove(coastal);

            context.SaveChanges();
            return;
        }

        // If Whitford Park already exists, do nothing.
        if (context.Courses.Any(
                c => c.Name == "Whitford Park Golf Club"))
        {
            return;
        }

        // Do not overwrite any other existing data.
        if (context.Courses.Any())
        {
            return;
        }

        // First-time database setup.
        var course = new Course
        {
            Name = "Whitford Park Golf Club",
            Location = "Whitford, Auckland, NZ"
        };

        AddHoles(
            course,
            new (int Par, int Distance)[]
            {
                (4, 270),
                (4, 323),
                (3, 107),
                (4, 327),
                (4, 347),
                (5, 410),
                (4, 326),
                (4, 370),
                (3, 180),
                (4, 297),
                (3, 140),
                (5, 428),
                (5, 460),
                (4, 310),
                (4, 288),
                (4, 293),
                (3, 117),
                (4, 354)
            });

        context.Courses.Add(course);
        context.SaveChanges();

        SeedRound(
            context,
            course,
            new DateTime(2026, 5, 3),
            "White",
            seed: 1,
            skillLevel: 1);

        SeedRound(
            context,
            course,
            new DateTime(2026, 5, 18),
            "White",
            seed: 2,
            skillLevel: 0);

        SeedRound(
            context,
            course,
            new DateTime(2026, 6, 8),
            "Blue",
            seed: 3,
            skillLevel: 0);

        SeedRound(
            context,
            course,
            new DateTime(2026, 6, 22),
            "White",
            seed: 4,
            skillLevel: -1);

        SeedRound(
            context,
            course,
            new DateTime(2026, 7, 12),
            "White",
            seed: 5,
            skillLevel: -1);

        context.SaveChanges();
    }

    private static void AddHoles(
        Course course,
        (int Par, int Distance)[] holes)
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

    private static void SeedRound(
        ApplicationDbContext context,
        Course course,
        DateTime date,
        string tee,
        int seed,
        int skillLevel)
    {
        var rnd = new Random(seed);

        var round = new Round
        {
            CourseId = course.Id,
            Date = date,
            Tee = tee
        };

        foreach (var ch in course.CourseHoles
                     .OrderBy(c => c.HoleNumber))
        {
            int offset =
                skillLevel
                + rnd.Next(0, 3)
                - (rnd.NextDouble() < 0.15 ? 1 : 0);

            int score = Math.Max(1, ch.Par + offset);

            int putts = Math.Clamp(
                2 + (score - ch.Par > 0
                    ? rnd.Next(0, 2)
                    : 0),
                1,
                4);

            bool gir =
                score <= ch.Par
                && rnd.NextDouble()
                    < (skillLevel <= 0 ? 0.55 : 0.35);

            bool? fairway =
                ch.Par == 3
                    ? null
                    : rnd.NextDouble()
                        < (skillLevel <= 0 ? 0.65 : 0.45);

            int penalty =
                rnd.NextDouble() < 0.1 ? 1 : 0;

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