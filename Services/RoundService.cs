using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public class RoundService : IRoundService
{
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
            .Include(r => r.CourseTee)
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
        if (query.From.HasValue) rounds = rounds.Where(r => r.Date >= ToUtc(query.From.Value));
        if (query.To.HasValue) rounds = rounds.Where(r => r.Date < ToUtc(query.To.Value).AddDays(1));
        if (query.HoleCount.HasValue) rounds = rounds.Where(r => r.Holes.Count == query.HoleCount.Value);
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<RoundStatus>(query.Status, true, out var status))
            rounds = rounds.Where(r => r.Status == status);

        var items = await rounds
            .Include(r => r.Course)
            .Include(r => r.CourseTee)
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
            CurrentHole = 1
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
        if (holeNumber is < 1 or > 18) return (null, "Hole number must be between 1 and 18.");

        var par = round.CourseTee?.CourseHoles.FirstOrDefault(h => h.HoleNumber == holeNumber)?.Par ?? dto.Par;
        var validation = ValidateHole(holeNumber, par, dto.Score, dto.Putts, dto.FairwayHit, dto.Penalty);
        if (validation is not null) return (null, validation);

        var hole = round.Holes.FirstOrDefault(h => h.HoleNumber == holeNumber);
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
        round.CurrentHole = Math.Min(ExpectedHoles(round), holeNumber + 1);
        round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (MapToHoleDto(hole), null);
    }

    public async Task<(RoundDetailDto? Round, string? Error)> CompleteAsync(int roundId)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes).Include(r => r.Course).Include(r => r.CourseTee)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null) return (null, "Round not found.");
        if (round.Status != RoundStatus.Draft) return (null, "Only a draft round can be completed.");
        var expected = ExpectedHoles(round);
        if (round.Holes.Count != expected || round.Holes.Select(h => h.HoleNumber).Distinct().Count() != expected)
            return (null, $"Record all {expected} holes before completing the round.");
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
            .Include(r => r.CourseTee)
            .Include(r => r.Holes)
            .FirstOrDefaultAsync();

        return round is null ? null : MapToDetailDto(round);
    }

    public async Task<(RoundDetailDto? Round, string? Error)> CreateAsync(
        RoundCreateDto dto)
    {
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

        var holes = new List<Hole>();
        foreach (var holeDto in dto.Holes)
        {
            if (holeDto.HoleNumber < 1 || holeDto.HoleNumber > 18)
                return (null, $"Hole {holeDto.HoleNumber} must be between 1 and 18.");

            var courseHole = holesByNumber.TryGetValue(holeDto.HoleNumber, out var mappedCourseHole)
                ? mappedCourseHole
                : null;
            var par = courseHole?.Par ?? holeDto.Par;
            if (par is null || par.Value < 3 || par.Value > 6)
                return (null, $"Par for hole {holeDto.HoleNumber} must be between 3 and 6.");

            if (holeDto.Score <= 0)
                return (null, $"Score for hole {holeDto.HoleNumber} must be greater than 0.");
            if (holeDto.Putts < 0)
                return (null, $"Putts for hole {holeDto.HoleNumber} cannot be negative.");
            if (holeDto.Penalty < 0)
                return (null, $"Penalty for hole {holeDto.HoleNumber} cannot be negative.");
            if (par.Value == 3 && holeDto.FairwayHit.HasValue)
                return (null, $"Hole {holeDto.HoleNumber} is a par 3 - fairway hit does not apply.");

            holes.Add(new Hole
            {
                HoleNumber = holeDto.HoleNumber,
                Par = par.Value,
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
    {
        var round = await _context.Rounds
            .Where(r => r.Id == id && r.UserId == CurrentUserId)
            .Include(r => r.Course)
                .ThenInclude(c => c!.CourseTees)
            .FirstOrDefaultAsync();

        if (round is null || round.Course is null)
            return false;

        var (tee, teeError) = ResolveTee(round.Course, dto.CourseTeeId, dto.Tee, true);
        if (teeError is not null)
            return false;

        round.CourseTee = tee;
        round.LegacyTee = IsCustomTee(tee!) ? tee!.Name : null;
        round.Date = ToUtc(dto.Date);
        await _context.SaveChangesAsync();
        return true;
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
        if (round.Holes.Any(h => h.HoleNumber == dto.HoleNumber))
            return (null, $"Hole {dto.HoleNumber} already exists for this round.");
        if (dto.HoleNumber < 1 || dto.HoleNumber > 18)
            return (null, $"Hole {dto.HoleNumber} must be between 1 and 18.");

        var courseHole = round.CourseTee?.CourseHoles
            .FirstOrDefault(h => h.HoleNumber == dto.HoleNumber);
        var par = courseHole?.Par ?? dto.Par;
        if (par is null || par.Value < 3 || par.Value > 6)
            return (null, $"Par for hole {dto.HoleNumber} must be between 3 and 6.");
        if (dto.Score <= 0) return (null, "Score must be greater than 0.");
        if (dto.Putts < 0) return (null, "Putts cannot be negative.");
        if (dto.Penalty < 0) return (null, "Penalty cannot be negative.");
        if (par.Value == 3 && dto.FairwayHit.HasValue)
            return (null, "Fairway hit does not apply to par 3 holes.");

        var hole = new Hole
        {
            RoundId = roundId,
            HoleNumber = dto.HoleNumber,
            Par = par.Value,
            Score = dto.Score,
            Putts = dto.Putts,
            GIR = dto.GIR,
            FairwayHit = dto.FairwayHit,
            Penalty = dto.Penalty
        };

        _context.Holes.Add(hole);
        await _context.SaveChangesAsync();
        return (MapToHoleDto(hole), null);
    }

    public async Task<(bool Success, string? Error)> UpdateHoleAsync(int roundId, int holeId, HoleUpdateDto dto)
    {
        var hole = await _context.Holes
            .Include(h => h.Round)
            .FirstOrDefaultAsync(h => h.Id == holeId && h.RoundId == roundId && h.Round!.UserId == CurrentUserId);
        if (hole is null) return (false, "Hole not found.");
        if (dto.Score <= 0) return (false, "Score must be greater than 0.");
        if (dto.Putts < 0) return (false, "Putts cannot be negative.");
        if (dto.Penalty < 0) return (false, "Penalty cannot be negative.");

        hole.Score = dto.Score;
        hole.Putts = dto.Putts;
        hole.GIR = dto.GIR;
        hole.FairwayHit = dto.FairwayHit;
        hole.Penalty = dto.Penalty;
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
            round.CurrentHole, ExpectedHoles(round), round.UpdatedAt);
    }

    private static int ExpectedHoles(Round round) => round.CourseTee?.NineHoles == true ? 9 : 18;

    private static string? ValidateHole(int holeNumber, int? par, int score, int putts, bool? fairwayHit, int penalty)
    {
        if (par is null or < 3 or > 6) return $"Par for hole {holeNumber} must be between 3 and 6.";
        if (score is < 1 or > 20) return $"Score for hole {holeNumber} must be between 1 and 20.";
        if (putts is < 0 or > 10) return $"Putts for hole {holeNumber} must be between 0 and 10.";
        if (penalty is < 0 or > 20) return $"Penalty for hole {holeNumber} must be between 0 and 20.";
        if (par == 3 && fairwayHit.HasValue) return $"Hole {holeNumber} is a par 3 - fairway hit does not apply.";
        return null;
    }

    private static HoleDto MapToHoleDto(Hole h) => new(
        h.Id, h.HoleNumber, h.Par, h.Score, h.Putts, h.GIR, h.FairwayHit, h.Penalty);
}
