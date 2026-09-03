using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

// Product heuristics, not a handicap calculation or a diagnosis of swing mechanics.
public static class PracticeInsights
{
    public static List<PerformanceInsightDto> Build(IEnumerable<Round> source)
    {
        var rounds = source.Where(r => r.Status == RoundStatus.Completed && r.Holes.Count is 9 or 18)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).ToList();
        if (rounds.Count < 3)
            return new() { new("more-data", "Build your baseline",
                $"{rounds.Count} of 3 completed 9/18-hole rounds recorded.",
                $"Complete {3 - rounds.Count} more rounds to unlock practice priorities. Partial rounds do not count.", "info") };

        var recent = rounds.Take(5).ToList();
        var holes = recent.SelectMany(r => r.Holes).ToList();
        var ids = recent.Select(r => r.Id).ToList();
        var period = $"{recent.Last().Date:yyyy-MM-dd} to {recent.First().Date:yyyy-MM-dd}";
        var result = new List<PerformanceInsightDto>();
        var putts = holes.Average(h => h.Putts);
        if (putts >= 2)
            result.Add(new("putting", "Make putting the next focus",
                $"{putts:F2} putts per hole over {holes.Count} holes in {recent.Count} rounds ({period}); threshold: 2.00.",
                "Practise distance control before short-putt repetition.", "high", ids,
                new("Putting distance ladder", 15, new() { "Choose three increasing distances on a practice green.",
                    "Putt five balls from each distance; count finishes within one putter length.",
                    "Finish with ten short putts and record makes." }, "Record close finishes out of 15 and short putts made out of 10.")));
        var gir = 100.0 * holes.Count(h => h.GIR) / holes.Count;
        if (gir < 40)
            result.Add(new("approach", "Create more birdie chances",
                $"GIR is {gir:F0}% ({holes.Count(h => h.GIR)}/{holes.Count} holes) over {recent.Count} rounds ({period}); threshold: below 40%.",
                "Use one repeatable approach distance and a clear target.", "medium", ids, ApproachDrill()));
        var penalties = holes.Sum(h => h.Penalty);
        var penaltiesPer18 = 18.0 * penalties / holes.Count;
        if (penaltiesPer18 > 1)
            result.Add(new("penalties", "Protect the scorecard",
                $"{penalties} penalty strokes / {holes.Count} holes = {penaltiesPer18:F2} per 18 holes ({period}); threshold: above 1.00.",
                "Practise a conservative tee-shot target and club choice.", "high", ids,
                new("Safe target rehearsal", 15, new() { "Choose a landing corridor on the range.",
                    "Hit ten shots with a comfortable tee club, using your full routine.",
                    "Count shots inside the corridor; compare with another club." }, "Record in-corridor shots out of 10 for each club.")));

        var eligible = holes.Where(h => h.Par != 3 && h.FairwayHit.HasValue).ToList();
        var fairwayHits = eligible.Count(h => h.FairwayHit == true);
        if (eligible.Count >= 18 && 100.0 * fairwayHits / eligible.Count < 40)
            result.Add(new("fairway", "Give the next shot a clearer starting point",
                $"Fairways hit: {fairwayHits}/{eligible.Count} ({100.0 * fairwayHits / eligible.Count:F0}%) ({period}). Par 3s and missing values excluded; minimum 18 observations, threshold below 40%.",
                "Use a landing corridor rather than maximum distance.", "medium", ids,
                new("Fairway corridor", 15, new() { "Pick a visible left and right boundary at the range.",
                    "Hit ten tee shots with the same club and routine.", "Record left, inside or right for each shot." }, "Count inside-corridor shots out of 10; repeat next session.")));

        if (rounds.Count < 6)
            result.Add(new("trend-baseline", "Unlock par-type comparisons",
                $"{rounds.Count}/6 completed 9/18-hole rounds available.",
                $"Record {6 - rounds.Count} more rounds. Comparisons use the latest 3 versus the previous 3, with at least 12 holes of a par type in each window.", "info"));
        else
        {
            var newer = rounds.Take(3).ToList();
            var older = rounds.Skip(3).Take(3).ToList();
            var sufficientTypes = 0;
            foreach (var par in new[] { 3, 4, 5 })
            {
                var current = newer.SelectMany(r => r.Holes).Where(h => h.Par == par).ToList();
                var previous = older.SelectMany(r => r.Holes).Where(h => h.Par == par).ToList();
                if (current.Count < 12 || previous.Count < 12) continue;
                sufficientTypes++;
                var currentRate = 100.0 * current.Count(h => h.GIR) / current.Count;
                var previousRate = 100.0 * previous.Count(h => h.GIR) / previous.Count;
                if (previousRate - currentRate < 15) continue;
                result.Add(new($"par-{par}-gir-decline", $"Check your par-{par} approaches",
                    $"Latest 3 ({newer.Last().Date:yyyy-MM-dd}–{newer.First().Date:yyyy-MM-dd}): {current.Count(h => h.GIR)}/{current.Count} GIR ({currentRate:F0}%). Previous 3 ({older.Last().Date:yyyy-MM-dd}–{older.First().Date:yyyy-MM-dd}): {previous.Count(h => h.GIR)}/{previous.Count} ({previousRate:F0}%). Down {previousRate - currentRate:F1} percentage points; threshold 15.",
                    "This is an observed change, not its cause. Course and tee difficulty may differ; review the scorecards before choosing a practice focus.", "medium",
                    newer.Concat(older).Select(r => r.Id).ToList(), ApproachDrill()));
            }
            if (sufficientTypes == 0)
                result.Add(new("trend-sample", "More par-type observations needed",
                    "The latest 3 and previous 3 rounds do not yet have 12 holes of the same par type in both windows.",
                    "Keep recording rounds. No decline is inferred from this small sample.", "info"));
        }
        if (result.All(x => x.Severity == "info"))
            result.Insert(0, new("steady", "Keep building your baseline",
                $"No evaluated priority threshold was crossed in the recent rounds ({period}). Missing observations can limit recommendations.",
                "Continue recording complete rounds; these rules do not assess every part of your game.", "info", ids));
        return result.OrderBy(x => x.Severity == "high" ? 0 : x.Severity == "medium" ? 1 : 2).ToList();
    }

    private static PracticeDrillDto ApproachDrill() => new("Approach target block", 20,
        new() { "Pick one comfortable approach club and a defined target area.",
            "Hit three sets of five balls, taking your normal pre-shot routine.",
            "Record target hits and the common miss direction after each set." },
        "Record target hits out of 15 and compare under similar conditions next session.");
}
