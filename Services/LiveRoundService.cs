using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public sealed class LiveRoundService : ILiveRoundService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public LiveRoundService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<ServiceResult<RoundDetailDto>> StartAsync(RoundStartDto dto)
    {
        if (dto.Date == default)
            return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundInvalid("A round date is required."));
        var course = await _context.Courses
            .Where(c => c.UserId == null || c.UserId == CurrentUserId)
            .Include(c => c.CourseTees).ThenInclude(t => t.CourseHoles)
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId);
        if (course is null) return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.CourseNotFound());

        var teeResult = RoundRules.ResolveTee(course, dto.CourseTeeId, dto.Tee, true);
        if (!teeResult.IsSuccess) return ServiceResult<RoundDetailDto>.Failure(teeResult.Error!);
        var tee = teeResult.Value!;
        var now = DateTime.UtcNow;
        var round = new Round
        {
            UserId = CurrentUserId,
            CourseId = course.Id,
            CourseTee = tee,
            LegacyTee = RoundRules.IsCustomTee(tee) ? tee.Name : null,
            Date = RoundRules.ToUtc(dto.Date),
            Status = RoundStatus.Draft,
            StartedAt = now,
            UpdatedAt = now,
            CurrentHole = RoundRules.ExpectedHoleNumbers(tee).First()
        };
        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();
        round.Course = course;
        return ServiceResult<RoundDetailDto>.Success(RoundRules.MapToDetailDto(round));
    }

    public async Task<ServiceResult<HoleDto>> UpsertHoleAsync(int roundId, int holeNumber, HoleUpsertDto dto)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null) return ServiceResult<HoleDto>.Failure(ServiceErrors.RoundNotFound());
        if (round.Status != RoundStatus.Draft)
            return ServiceResult<HoleDto>.Failure(ServiceErrors.HoleInvalid("Only a draft round can be edited live."));
        var expectedNumbers = RoundRules.ExpectedHoleNumbers(round.CourseTee);
        if (!expectedNumbers.Contains(holeNumber))
            return ServiceResult<HoleDto>.Failure(ServiceErrors.HoleInvalid(
                "This hole does not belong to the selected layout."));

        var par = round.CourseTee?.CourseHoles.FirstOrDefault(h => h.HoleNumber == holeNumber)?.Par ?? dto.Par;
        var validation = RoundRules.ValidateHole(holeNumber, par, dto.Score, dto.Putts, dto.FairwayHit, dto.Penalty);
        if (validation is not null) return ServiceResult<HoleDto>.Failure(validation);

        var hole = round.Holes.FirstOrDefault(h => h.HoleNumber == holeNumber);
        if (dto.CheckExpected)
        {
            var actual = hole is null ? null : RoundRules.MapToHoleDto(hole);
            if (actual is not null && actual.Par == par && actual.Score == dto.Score && actual.Putts == dto.Putts &&
                actual.GIR == dto.GIR && actual.FairwayHit == dto.FairwayHit && actual.Penalty == dto.Penalty)
                return ServiceResult<HoleDto>.Success(actual);
            if (actual != dto.ExpectedHole)
                return ServiceResult<HoleDto>.Failure(ServiceErrors.RoundConflict());
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
        round.CurrentHole = holeNumber;
        round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ServiceResult<HoleDto>.Success(RoundRules.MapToHoleDto(hole));
    }

    public async Task<ServiceResult<HoleDto>> AddHoleAsync(int roundId, HoleCreateDto dto)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes)
            .Include(r => r.Course).ThenInclude(c => c!.CourseTees).ThenInclude(t => t.CourseHoles)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null) return ServiceResult<HoleDto>.Failure(ServiceErrors.RoundNotFound());
        if (round.Status != RoundStatus.Draft)
            return ServiceResult<HoleDto>.Failure(ServiceErrors.HoleInvalid(
                "Only a draft round can accept new holes."));
        if (round.Holes.Any(h => h.HoleNumber == dto.HoleNumber))
            return ServiceResult<HoleDto>.Failure(ServiceErrors.HoleInvalid(
                $"Hole {dto.HoleNumber} already exists for this round."));
        if (!RoundRules.ExpectedHoleNumbers(round.CourseTee).Contains(dto.HoleNumber))
            return ServiceResult<HoleDto>.Failure(ServiceErrors.HoleInvalid(
                "This hole does not belong to the selected layout."));

        var courseHole = round.CourseTee?.CourseHoles.FirstOrDefault(h => h.HoleNumber == dto.HoleNumber);
        var par = courseHole?.Par ?? dto.Par;
        var validation = RoundRules.ValidateHole(dto.HoleNumber, par, dto.Score, dto.Putts, dto.FairwayHit, dto.Penalty);
        if (validation is not null) return ServiceResult<HoleDto>.Failure(validation);

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
        return ServiceResult<HoleDto>.Success(RoundRules.MapToHoleDto(hole));
    }
}
