using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public class RoundService : IRoundService
{
    private readonly ApplicationDbContext _context;

    public RoundService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RoundSummaryDto>> GetAllAsync()
    {
        var rounds = await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Holes)
            .OrderByDescending(r => r.Date)
            .ToListAsync();

        return rounds.Select(ToSummaryDto).ToList();
    }

    public async Task<RoundDetailDto?> GetByIdAsync(int id)
    {
        var round = await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Holes)
            .FirstOrDefaultAsync(r => r.Id == id);

        return round is null ? null : MapToDetailDto(round);
    }

    public async Task<(RoundDetailDto? Round, string? Error)> CreateAsync(
        RoundCreateDto dto)
    {
        var course = await _context.Courses
            .Include(c => c.CourseHoles)
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId);

        if (course is null)
            return (null, "Course not found.");

        if (dto.Holes.Select(h => h.HoleNumber).Distinct().Count()
            != dto.Holes.Count)
        {
            return (null, "Duplicate hole numbers submitted.");
        }

        var holesByNumber = course.CourseHoles
            .ToDictionary(h => h.HoleNumber);

        var holes = new List<Hole>();

        foreach (var holeDto in dto.Holes)
        {
            if (holeDto.HoleNumber < 1 || holeDto.HoleNumber > 18)
            {
                return (null, $"Hole {holeDto.HoleNumber} must be between 1 and 18.");
            }

            var courseHole = holesByNumber.TryGetValue(holeDto.HoleNumber, out var mappedCourseHole)
                ? mappedCourseHole
                : null;
            var par = courseHole?.Par ?? holeDto.Par;

            if (par is null || par.Value < 3 || par.Value > 6)
            {
                return (null, $"Par for hole {holeDto.HoleNumber} must be between 3 and 6.");
            }

            if (holeDto.Score <= 0)
            {
                return (
                    null,
                    $"Score for hole {holeDto.HoleNumber} must be greater than 0.");
            }

            if (holeDto.Putts < 0)
            {
                return (
                    null,
                    $"Putts for hole {holeDto.HoleNumber} cannot be negative.");
            }

            if (holeDto.Penalty < 0)
            {
                return (
                    null,
                    $"Penalty for hole {holeDto.HoleNumber} cannot be negative.");
            }

            if (par.Value == 3 &&
                holeDto.FairwayHit.HasValue)
            {
                return (
                    null,
                    $"Hole {holeDto.HoleNumber} is a par 3 - " +
                    "fairway hit does not apply.");
            }

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
            CourseId = dto.CourseId,

            // PostgreSQL timestamp with time zone requires UTC.
            Date = ToUtc(dto.Date),

            Tee = dto.Tee,
            Holes = holes
        };

        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();

        round.Course = course;

        return (MapToDetailDto(round), null);
    }

    public async Task<bool> UpdateAsync(
        int id,
        RoundUpdateDto dto)
    {
        var round = await _context.Rounds.FindAsync(id);

        if (round is null)
            return false;

        round.Date = ToUtc(dto.Date);
        round.Tee = dto.Tee;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var round = await _context.Rounds.FindAsync(id);

        if (round is null)
            return false;

        _context.Rounds.Remove(round);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<HoleDto>?> GetHolesAsync(int roundId)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes)
            .FirstOrDefaultAsync(r => r.Id == roundId);

        if (round is null)
            return null;

        return round.Holes
            .OrderBy(h => h.HoleNumber)
            .Select(MapToHoleDto)
            .ToList();
    }

    public async Task<(HoleDto? Hole, string? Error)> AddHoleAsync(
        int roundId,
        HoleCreateDto dto)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes)
            .Include(r => r.Course)
                .ThenInclude(c => c!.CourseHoles)
            .FirstOrDefaultAsync(r => r.Id == roundId);

        if (round is null)
            return (null, "Round not found.");

        if (round.Holes.Any(h => h.HoleNumber == dto.HoleNumber))
        {
            return (
                null,
                $"Hole {dto.HoleNumber} already exists for this round.");
        }

        if (dto.HoleNumber < 1 || dto.HoleNumber > 18)
            return (null, $"Hole {dto.HoleNumber} must be between 1 and 18.");

        var courseHole = round.Course?.CourseHoles
            .FirstOrDefault(h => h.HoleNumber == dto.HoleNumber);
        var par = courseHole?.Par ?? dto.Par;

        if (par is null || par.Value < 3 || par.Value > 6)
        {
            return (null, $"Par for hole {dto.HoleNumber} must be between 3 and 6.");
        }

        if (dto.Score <= 0)
            return (null, "Score must be greater than 0.");

        if (dto.Putts < 0)
            return (null, "Putts cannot be negative.");

        if (dto.Penalty < 0)
            return (null, "Penalty cannot be negative.");

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

    public async Task<(bool Success, string? Error)> UpdateHoleAsync(
        int roundId,
        int holeId,
        HoleUpdateDto dto)
    {
        var hole = await _context.Holes
            .FirstOrDefaultAsync(
                h => h.Id == holeId &&
                     h.RoundId == roundId);

        if (hole is null)
            return (false, "Hole not found.");

        if (dto.Score <= 0)
            return (false, "Score must be greater than 0.");

        if (dto.Putts < 0)
            return (false, "Putts cannot be negative.");

        if (dto.Penalty < 0)
            return (false, "Penalty cannot be negative.");

        hole.Score = dto.Score;
        hole.Putts = dto.Putts;
        hole.GIR = dto.GIR;
        hole.FairwayHit = dto.FairwayHit;
        hole.Penalty = dto.Penalty;

        await _context.SaveChangesAsync();

        return (true, null);
    }

    private static DateTime ToUtc(DateTime date)
    {
        if (date.Kind == DateTimeKind.Utc)
            return date;

        return DateTime.SpecifyKind(
            date,
            DateTimeKind.Utc);
    }

    private static RoundSummaryDto ToSummaryDto(Round r)
    {
        int totalScore = r.Holes.Sum(h => h.Score);
        int totalPar = r.Holes.Sum(h => h.Par);

        return new RoundSummaryDto(
            r.Id,
            r.CourseId,
            r.Course?.Name ?? "",
            r.Date,
            r.Tee,
            totalScore,
            totalScore - totalPar);
    }

    private static RoundDetailDto MapToDetailDto(
        Round round)
    {
        var holes = round.Holes
            .OrderBy(h => h.HoleNumber)
            .Select(MapToHoleDto)
            .ToList();

        return new RoundDetailDto(
            round.Id,
            round.CourseId,
            round.Course?.Name ?? "",
            round.Date,
            round.Tee,
            holes);
    }

    private static HoleDto MapToHoleDto(Hole h)
    {
        return new HoleDto(
            h.Id,
            h.HoleNumber,
            h.Par,
            h.Score,
            h.Putts,
            h.GIR,
            h.FairwayHit,
            h.Penalty);
    }
}