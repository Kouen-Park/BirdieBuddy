using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

internal static class RoundRules
{
    internal static ServiceResult<CourseTee> ResolveTee(
        Course course, int? requestedTeeId, string? requestedTeeName, bool createCustom)
    {
        if (requestedTeeId.HasValue)
        {
            var selected = course.CourseTees.FirstOrDefault(t => t.Id == requestedTeeId.Value);
            return selected is null
                ? ServiceResult<CourseTee>.Failure(ServiceErrors.RoundInvalid(
                    "The selected tee does not belong to this course."))
                : ServiceResult<CourseTee>.Success(selected);
        }

        if (!string.IsNullOrWhiteSpace(requestedTeeName))
        {
            var selected = course.CourseTees.FirstOrDefault(t =>
                string.Equals(t.Name, requestedTeeName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selected is not null) return ServiceResult<CourseTee>.Success(selected);

            if (createCustom)
            {
                var custom = new CourseTee
                {
                    CourseId = course.Id,
                    Name = requestedTeeName.Trim(),
                    CourseType = "CUSTOM",
                    Gender = string.Empty,
                    NineHoles = false
                };
                course.CourseTees.Add(custom);
                return ServiceResult<CourseTee>.Success(custom);
            }
        }

        var fallback = course.CourseTees.OrderBy(t => t.NineHoles).ThenBy(t => t.Name).FirstOrDefault();
        if (fallback is not null) return ServiceResult<CourseTee>.Success(fallback);
        if (!createCustom)
            return ServiceResult<CourseTee>.Failure(ServiceErrors.RoundInvalid(
                "This course has no tee configuration."));

        var defaultTee = new CourseTee
        {
            CourseId = course.Id,
            Name = "Default",
            CourseType = "CUSTOM",
            Gender = string.Empty,
            NineHoles = false
        };
        course.CourseTees.Add(defaultTee);
        return ServiceResult<CourseTee>.Success(defaultTee);
    }

    internal static bool IsCustomTee(CourseTee tee) =>
        string.Equals(tee.CourseType, "CUSTOM", StringComparison.OrdinalIgnoreCase);

    internal static DateTime ToUtc(DateTime date) => date.Kind == DateTimeKind.Utc
        ? date
        : DateTime.SpecifyKind(date, DateTimeKind.Utc);

    internal static DateTime ToUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    internal static RoundSummaryDto ToSummaryDto(Round round)
    {
        var totalScore = round.Holes.Sum(h => h.Score);
        var totalPar = round.Holes.Sum(h => h.Par);
        return new RoundSummaryDto(
            round.Id, round.CourseId, round.Course?.Name ?? "", DateOnly.FromDateTime(round.Date),
            round.CourseTeeId, TeeLabel(round), totalScore, totalScore - totalPar,
            round.Status.ToString(), round.Holes.Count, ExpectedHoleNumbers(round.CourseTee).Count);
    }

    internal static RoundDetailDto MapToDetailDto(Round round)
    {
        var holes = round.Holes.OrderBy(h => h.HoleNumber).Select(MapToHoleDto).ToList();
        return new RoundDetailDto(
            round.Id, round.CourseId, round.Course?.Name ?? "", DateOnly.FromDateTime(round.Date),
            round.CourseTeeId, TeeLabel(round), holes, round.Status.ToString(),
            round.CurrentHole, ExpectedHoleNumbers(round.CourseTee).Count, round.UpdatedAt,
            ExpectedHoleNumbers(round.CourseTee));
    }

    internal static List<int> ExpectedHoleNumbers(CourseTee? tee) =>
        tee?.CourseHoles.Count > 0
            ? tee.CourseHoles.Select(h => h.HoleNumber).Distinct().OrderBy(n => n).ToList()
            : Enumerable.Range(1, tee?.NineHoles == true ? 9 : 18).ToList();

    internal static ServiceError? ValidateHole(
        int holeNumber, int? par, int score, int putts, bool? fairwayHit, int penalty)
    {
        if (par is null or < 3 or > 6)
            return ServiceErrors.HoleInvalid($"Par for hole {holeNumber} must be between 3 and 6.");
        if (score is < 1 or > 20)
            return ServiceErrors.HoleInvalid($"Score for hole {holeNumber} must be between 1 and 20.");
        if (putts is < 0 or > 10)
            return ServiceErrors.HoleInvalid($"Putts for hole {holeNumber} must be between 0 and 10.");
        if (penalty is < 0 or > 20)
            return ServiceErrors.HoleInvalid($"Penalty for hole {holeNumber} must be between 0 and 20.");
        if (putts + penalty > score)
            return ServiceErrors.HoleInvalid($"Putts and penalties for hole {holeNumber} cannot exceed the score.");
        if (par == 3 && fairwayHit.HasValue)
            return ServiceErrors.HoleInvalid($"Hole {holeNumber} is a par 3 - fairway hit does not apply.");
        return null;
    }

    internal static HoleDto MapToHoleDto(Hole hole) => new(
        hole.Id, hole.HoleNumber, hole.Par, hole.Score, hole.Putts, hole.GIR, hole.FairwayHit, hole.Penalty);

    private static string TeeLabel(Round round) =>
        !string.IsNullOrWhiteSpace(round.LegacyTee)
            ? round.LegacyTee!
            : round.CourseTee?.Name ?? "Unknown";
}
