using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public class RoundService : IRoundService
{
    public const string ConflictMessage = "This round changed in another session. Review the server record before retrying.";
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public RoundService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<List<RoundSummaryDto>> GetAllAsync()
    {
        var rounds = await _context.Rounds
            .Where(r => r.UserId == CurrentUserId)
            .Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .Include(r => r.Holes)
            .OrderByDescending(r => r.Date)
            .ToListAsync();

        return rounds.Select(ToSummaryDto).ToList();
    }

    public async Task<RoundPageDto> GetPageAsync(RoundQueryDto query)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var rounds = _context.Rounds
            .AsNoTracking()
            .Where(r => r.UserId == CurrentUserId);

        if (query.Cursor.HasValue) rounds = rounds.Where(r => r.Id < query.Cursor.Value);
        if (query.CourseId.HasValue) rounds = rounds.Where(r => r.CourseId == query.CourseId.Value);
        if (query.CourseTeeId.HasValue) rounds = rounds.Where(r => r.CourseTeeId == query.CourseTeeId.Value);
        if (query.From.HasValue) rounds = rounds.Where(r => r.Date >= ToUtc(query.From.Value));
        if (query.To.HasValue) rounds = rounds.Where(r => r.Date < ToUtc(query.To.Value).AddDays(1));
        if (query.HoleCount.HasValue) rounds = rounds.Where(r => r.Holes.Count == query.HoleCount.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rounds = rounds.Where(r => (r.Course != null && r.Course.Name.Contains(search)) ||
                (r.LegacyTee != null && r.LegacyTee.Contains(search)) ||
                (r.CourseTee != null && r.CourseTee.Name.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<RoundStatus>(query.Status, true, out var status))
            rounds = rounds.Where(r => r.Status == status);

        var items = await rounds
            .Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .Include(r => r.Holes)
            .OrderByDescending(r => r.Id)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new RoundPageDto(items.Select(ToSummaryDto).ToList(), hasMore ? items[^1].Id : null);
    }

    public async Task<(RoundDetailDto? Round, string? Error)> StartAsync(RoundStartDto dto)
    {
        if (dto.Date == default) return (null, "A round date is required.");
        var course = await _context.Courses
            .Where(c => c.UserId == null || c.UserId == CurrentUserId)
            .Include(c => c.CourseTees).ThenInclude(t => t.CourseHoles)
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId);
        if (course is null) return (null, "Course not found.");

        var (tee, error) = ResolveTee(course, dto.CourseTeeId, dto.Tee, true);
        if (error is not null) return (null, error);
        var now = DateTime.UtcNow;
        var round = new Round
        {
            UserId = CurrentUserId,
            CourseId = course.Id,
            CourseTee = tee,
            LegacyTee = IsCustomTee(tee!) ? tee!.Name : null,
            Date = ToUtc(dto.Date),
            Status = RoundStatus.Draft,
            StartedAt = now,
            UpdatedAt = now,
            CurrentHole = ExpectedHoleNumbers(tee).First()
        };
        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();
        round.Course = course;
        return (MapToDetailDto(round), null);
    }

    public async Task<(HoleDto? Hole, string? Error)> UpsertHoleAsync(int roundId, int holeNumber, HoleUpsertDto dto)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null) return (null, "Round not found.");
        if (round.Status != RoundStatus.Draft) return (null, "Only a draft round can be edited live.");
        var expectedNumbers = ExpectedHoleNumbers(round.CourseTee);
        if (!expectedNumbers.Contains(holeNumber)) return (null, "This hole does not belong to the selected layout.");

        var par = round.CourseTee?.CourseHoles.FirstOrDefault(h => h.HoleNumber == holeNumber)?.Par ?? dto.Par;
        var validation = ValidateHole(holeNumber, par, dto.Score, dto.Putts, dto.FairwayHit, dto.Penalty);
        if (validation is not null) return (null, validation);

        var hole = round.Holes.FirstOrDefault(h => h.HoleNumber == holeNumber);
        if (dto.CheckExpected)
        {
            var actual = hole is null ? null : MapToHoleDto(hole);
            // An acknowledged request may be retried after its response was lost.
            if (actual is not null && actual.Par == par && actual.Score == dto.Score && actual.Putts == dto.Putts &&
                actual.GIR == dto.GIR && actual.FairwayHit == dto.FairwayHit && actual.Penalty == dto.Penalty)
                return (actual, null);
            if (actual != dto.ExpectedHole) return (null, ConflictMessage);
        }
        if (hole is null)
        {
            hole = new Hole { RoundId = roundId, HoleNumber = holeNumber };
            round.Holes.Add(hole);
        }
        hole.Par = par!.Value;
        hole.Score = dto.Score;
        hole.Putts = dto.Putts;
        hole.GIR = dto.GIR;
        hole.FairwayHit = par == 3 ? null : dto.FairwayHit;
        hole.Penalty = dto.Penalty;
        // Saving does not imply navigation. Reopening returns to the last edited hole.
        round.CurrentHole = holeNumber;
        round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (MapToHoleDto(hole), null);
    }

    public async Task<(RoundDetailDto? Round, string? Error)> CompleteAsync(int roundId)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes).Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null) return (null, "Round not found.");
        if (round.Status != RoundStatus.Draft) return (null, "Only a draft round can be completed.");
        var expected = ExpectedHoleNumbers(round.CourseTee);
        if (round.Holes.Count != expected.Count || !expected.SequenceEqual(round.Holes.Select(h => h.HoleNumber).OrderBy(n => n)))
            return (null, $"Record all {expected.Count} holes in the selected layout before completing the round.");
        foreach (var hole in round.Holes)
        {
            var validation = ValidateHole(hole.HoleNumber, hole.Par, hole.Score, hole.Putts, hole.FairwayHit, hole.Penalty);
            if (validation is not null) return (null, validation);
        }
        round.Status = RoundStatus.Completed;
        round.CompletedAt = DateTime.UtcNow;
        round.UpdatedAt = round.CompletedAt.Value;
        await _context.SaveChangesAsync();
        return (MapToDetailDto(round), null);
    }

    public async Task<bool> AbandonAsync(int roundId)
    {
        var round = await _context.Rounds.FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null || round.Status != RoundStatus.Draft) return false;
        round.Status = RoundStatus.Abandoned;
        round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<RoundDetailDto?> GetByIdAsync(int id)
    {
        var round = await _context.Rounds
            .Where(r => r.Id == id && r.UserId == CurrentUserId)
            .Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .Include(r => r.Holes)
            .FirstOrDefaultAsync();

        return round is null ? null : MapToDetailDto(round);
    }

    public async Task<(RoundDetailDto? Round, string? Error)> CreateAsync(
        RoundCreateDto dto)
    {
        if (dto.Date == default) return (null, "A round date is required.");
        if (dto.Holes is null || dto.Holes.Count == 0) return (null, "Record the scorecard before completing a round.");
        var course = await _context.Courses
            .Where(c => c.UserId == null || c.UserId == CurrentUserId)
            .Include(c => c.CourseTees)
                .ThenInclude(t => t.CourseHoles)
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId);

        if (course is null)
            return (null, "Course not found.");

        var (tee, teeError) = ResolveTee(course, dto.CourseTeeId, dto.Tee, true);
        if (teeError is not null)
            return (null, teeError);

        var holesByNumber = tee!.CourseHoles
            .GroupBy(h => h.HoleNumber)
            .ToDictionary(g => g.Key, g => g.First());

        if (dto.Holes.Select(h => h.HoleNumber).Distinct().Count() != dto.Holes.Count)
            return (null, "Duplicate hole numbers submitted.");

        var expected = ExpectedHoleNumbers(tee);
        if (!expected.SequenceEqual(dto.Holes.Select(h => h.HoleNumber).OrderBy(n => n)))
            return (null, $"Record all {expected.Count} holes in the selected layout before completing the round.");

        var holes = new List<Hole>();
        foreach (var holeDto in dto.Holes)
        {
            if (holeDto.HoleNumber < 1 || holeDto.HoleNumber > 18)
                return (null, $"Hole {holeDto.HoleNumber} must be between 1 and 18.");

            var courseHole = holesByNumber.TryGetValue(holeDto.HoleNumber, out var mappedCourseHole)
                ? mappedCourseHole
                : null;
            var par = courseHole?.Par ?? holeDto.Par;
            var validation = ValidateHole(holeDto.HoleNumber, par, holeDto.Score, holeDto.Putts, holeDto.FairwayHit, holeDto.Penalty);
            if (validation is not null) return (null, validation);

            holes.Add(new Hole
            {
                HoleNumber = holeDto.HoleNumber,
                Par = par!.Value,
                Score = holeDto.Score,
                Putts = holeDto.Putts,
                GIR = holeDto.GIR,
                FairwayHit = holeDto.FairwayHit,
                Penalty = holeDto.Penalty
            });
        }

        var round = new Round
        {
            UserId = CurrentUserId,
            CourseId = dto.CourseId,
            CourseTee = tee,
            LegacyTee = IsCustomTee(tee) ? tee.Name : null,
            Date = ToUtc(dto.Date),
            Holes = holes,
            Status = RoundStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();
        round.Course = course;
        return (MapToDetailDto(round), null);
    }

    public async Task<bool> UpdateAsync(int id, RoundUpdateDto dto)
        => (await UpdateWithErrorAsync(id, dto)).Success;

    public async Task<(bool Success, string? Error)> UpdateWithErrorAsync(int id, RoundUpdateDto dto)
    {
        var round = await _context.Rounds
            .Where(r => r.Id == id && r.UserId == CurrentUserId)
            .Include(r => r.Course)
                .ThenInclude(c => c!.CourseTees)
            .FirstOrDefaultAsync();

        if (round is null || round.Course is null || round.Status == RoundStatus.Abandoned || dto.Date == default)
            return (false, "Round not found.");

        if (dto.ExpectedUpdatedAt.HasValue && round.UpdatedAt != dto.ExpectedUpdatedAt.Value)
            return (false, ConflictMessage);

        var (tee, teeError) = ResolveTee(round.Course, dto.CourseTeeId, dto.Tee, true);
        if (teeError is not null)
            return (false, teeError);

        // Reassigning a tee without migrating the score snapshots would corrupt history.
        if (round.CourseTeeId != tee!.Id && await _context.Holes.AnyAsync(h => h.RoundId == id))
            return (false, "A round with recorded holes cannot change tees.");

        round.CourseTee = tee;
        round.LegacyTee = IsCustomTee(tee!) ? tee!.Name : null;
        round.Date = ToUtc(dto.Date);
        round.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, ConflictMessage);
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == CurrentUserId);
        if (round is null) return false;

        _context.Rounds.Remove(round);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<HoleDto>?> GetHolesAsync(int roundId)
    {
        var round = await _context.Rounds
            .Where(r => r.Id == roundId && r.UserId == CurrentUserId)
            .Include(r => r.Holes)
            .FirstOrDefaultAsync();

        return round is null
            ? null
            : round.Holes.OrderBy(h => h.HoleNumber).Select(MapToHoleDto).ToList();
    }

    public async Task<(HoleDto? Hole, string? Error)> AddHoleAsync(int roundId, HoleCreateDto dto)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes)
            .Include(r => r.Course)
                .ThenInclude(c => c!.CourseTees)
                    .ThenInclude(t => t.CourseHoles)
            .Include(r => r.CourseTee)
                .ThenInclude(t => t!.CourseHoles)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);

        if (round is null) return (null, "Round not found.");
        if (round.Status != RoundStatus.Draft) return (null, "Only a draft round can accept new holes.");
        if (round.Holes.Any(h => h.HoleNumber == dto.HoleNumber))
            return (null, $"Hole {dto.HoleNumber} already exists for this round.");
        if (!ExpectedHoleNumbers(round.CourseTee).Contains(dto.HoleNumber))
            return (null, "This hole does not belong to the selected layout.");

        var courseHole = round.CourseTee?.CourseHoles
            .FirstOrDefault(h => h.HoleNumber == dto.HoleNumber);
        var par = courseHole?.Par ?? dto.Par;
        var validation = ValidateHole(dto.HoleNumber, par, dto.Score, dto.Putts, dto.FairwayHit, dto.Penalty);
        if (validation is not null) return (null, validation);

        var hole = new Hole
        {
            RoundId = roundId,
            HoleNumber = dto.HoleNumber,
            Par = par!.Value,
            Score = dto.Score,
            Putts = dto.Putts,
            GIR = dto.GIR,
            FairwayHit = dto.FairwayHit,
            Penalty = dto.Penalty
        };

        _context.Holes.Add(hole);
        round.CurrentHole = dto.HoleNumber;
        round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (MapToHoleDto(hole), null);
    }

    public async Task<(bool Success, string? Error)> UpdateHoleAsync(int roundId, int holeId, HoleUpdateDto dto)
    {
        var hole = await _context.Holes
            .Include(h => h.Round)
            .FirstOrDefaultAsync(h => h.Id == holeId && h.RoundId == roundId && h.Round!.UserId == CurrentUserId);
        if (hole is null) return (false, "Hole not found.");
        if (hole.Round!.Status != RoundStatus.Completed)
            return (false, "Use live entry for draft rounds; abandoned rounds cannot be edited.");
        var validation = ValidateHole(hole.HoleNumber, hole.Par, dto.Score, dto.Putts, dto.FairwayHit, dto.Penalty);
        if (validation is not null) return (false, validation);

        if (dto.CheckExpected && dto.ExpectedHole != new HoleDto(hole.Id, hole.HoleNumber, hole.Par,
            hole.Score, hole.Putts, hole.GIR, hole.FairwayHit, hole.Penalty))
        {
            if (hole.Score == dto.Score && hole.Putts == dto.Putts && hole.GIR == dto.GIR &&
                hole.FairwayHit == dto.FairwayHit && hole.Penalty == dto.Penalty)
                return (true, null);
            return (false, ConflictMessage);
        }

        hole.Score = dto.Score;
        hole.Putts = dto.Putts;
        hole.GIR = dto.GIR;
        hole.FairwayHit = dto.FairwayHit;
        hole.Penalty = dto.Penalty;
        hole.Round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    private static (CourseTee? Tee, string? Error) ResolveTee(
        Course course,
        int? requestedTeeId,
        string? requestedTeeName,
        bool createCustom)
    {
        if (requestedTeeId.HasValue)
        {
            var selected = course.CourseTees.FirstOrDefault(t => t.Id == requestedTeeId.Value);
            return selected is null
                ? (null, "The selected tee does not belong to this course.")
                : (selected, null);
        }

        if (!string.IsNullOrWhiteSpace(requestedTeeName))
        {
            var selected = course.CourseTees.FirstOrDefault(t =>
                string.Equals(t.Name, requestedTeeName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selected is not null) return (selected, null);

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
                return (custom, null);
            }
        }

        var fallback = course.CourseTees
            .OrderBy(t => t.NineHoles)
            .ThenBy(t => t.Name)
            .FirstOrDefault();
        if (fallback is not null) return (fallback, null);

        if (!createCustom) return (null, "This course has no tee configuration.");

        var defaultTee = new CourseTee
        {
            CourseId = course.Id,
            Name = "Default",
            CourseType = "CUSTOM",
            Gender = string.Empty,
            NineHoles = false
        };
        course.CourseTees.Add(defaultTee);
        return (defaultTee, null);
    }

    private static bool IsCustomTee(CourseTee tee) =>
        string.Equals(tee.CourseType, "CUSTOM", StringComparison.OrdinalIgnoreCase);

    private static DateTime ToUtc(DateTime date) => date.Kind == DateTimeKind.Utc
        ? date
        : DateTime.SpecifyKind(date, DateTimeKind.Utc);

    private static DateTime ToUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static string TeeLabel(Round round) =>
        !string.IsNullOrWhiteSpace(round.LegacyTee)
            ? round.LegacyTee!
            : round.CourseTee?.Name ?? "Unknown";

    private static RoundSummaryDto ToSummaryDto(Round r)
    {
        int totalScore = r.Holes.Sum(h => h.Score);
        int totalPar = r.Holes.Sum(h => h.Par);
        return new RoundSummaryDto(
            r.Id, r.CourseId, r.Course?.Name ?? "", DateOnly.FromDateTime(r.Date),
            r.CourseTeeId, TeeLabel(r), totalScore, totalScore - totalPar,
            r.Status.ToString(), r.Holes.Count, ExpectedHoles(r));
    }

    private static RoundDetailDto MapToDetailDto(Round round)
    {
        var holes = round.Holes.OrderBy(h => h.HoleNumber).Select(MapToHoleDto).ToList();
        return new RoundDetailDto(
            round.Id, round.CourseId, round.Course?.Name ?? "", DateOnly.FromDateTime(round.Date),
            round.CourseTeeId, TeeLabel(round), holes, round.Status.ToString(),
            round.CurrentHole, ExpectedHoles(round), round.UpdatedAt, ExpectedHoleNumbers(round.CourseTee));
    }

    private static List<int> ExpectedHoleNumbers(CourseTee? tee) =>
        tee?.CourseHoles.Count > 0
            ? tee.CourseHoles.Select(h => h.HoleNumber).Distinct().OrderBy(n => n).ToList()
            : Enumerable.Range(1, tee?.NineHoles == true ? 9 : 18).ToList();

    private static int ExpectedHoles(Round round) => ExpectedHoleNumbers(round.CourseTee).Count;

    private static string? ValidateHole(int holeNumber, int? par, int score, int putts, bool? fairwayHit, int penalty)
    {
        if (par is null or < 3 or > 6) return $"Par for hole {holeNumber} must be between 3 and 6.";
        if (score is < 1 or > 20) return $"Score for hole {holeNumber} must be between 1 and 20.";
        if (putts is < 0 or > 10) return $"Putts for hole {holeNumber} must be between 0 and 10.";
        if (penalty is < 0 or > 20) return $"Penalty for hole {holeNumber} must be between 0 and 20.";
        if (putts + penalty > score) return $"Putts and penalties for hole {holeNumber} cannot exceed the score.";
        if (par == 3 && fairwayHit.HasValue) return $"Hole {holeNumber} is a par 3 - fairway hit does not apply.";
        return null;
    }

    private static HoleDto MapToHoleDto(Hole h) => new(
        h.Id, h.HoleNumber, h.Par, h.Score, h.Putts, h.GIR, h.FairwayHit, h.Penalty);
}
