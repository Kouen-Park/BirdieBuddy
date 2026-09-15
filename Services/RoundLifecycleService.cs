using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public sealed class RoundLifecycleService : IRoundLifecycleService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public RoundLifecycleService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<ServiceResult<RoundDetailDto>> CreateAsync(RoundCreateDto dto)
    {
        if (dto.Date == default)
            return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundInvalid("A round date is required."));
        if (dto.Holes is null || dto.Holes.Count == 0)
            return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundInvalid(
                "Record the scorecard before completing a round."));
        var course = await _context.Courses
            .Where(c => c.UserId == null || c.UserId == CurrentUserId)
            .Include(c => c.CourseTees).ThenInclude(t => t.CourseHoles)
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId);
        if (course is null) return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.CourseNotFound());

        var teeResult = RoundRules.ResolveTee(course, dto.CourseTeeId, dto.Tee, true);
        if (!teeResult.IsSuccess) return ServiceResult<RoundDetailDto>.Failure(teeResult.Error!);
        var tee = teeResult.Value!;
        var holesByNumber = tee.CourseHoles.GroupBy(h => h.HoleNumber).ToDictionary(g => g.Key, g => g.First());
        if (dto.Holes.Select(h => h.HoleNumber).Distinct().Count() != dto.Holes.Count)
            return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundInvalid("Duplicate hole numbers submitted."));
        var expected = RoundRules.ExpectedHoleNumbers(tee);
        if (!expected.SequenceEqual(dto.Holes.Select(h => h.HoleNumber).OrderBy(n => n)))
            return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundInvalid(
                $"Record all {expected.Count} holes in the selected layout before completing the round."));

        var holes = new List<Hole>();
        foreach (var holeDto in dto.Holes)
        {
            if (holeDto.HoleNumber is < 1 or > 18)
                return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.HoleInvalid(
                    $"Hole {holeDto.HoleNumber} must be between 1 and 18."));
            var courseHole = holesByNumber.TryGetValue(holeDto.HoleNumber, out var mapped) ? mapped : null;
            var par = courseHole?.Par ?? holeDto.Par;
            var validation = RoundRules.ValidateHole(
                holeDto.HoleNumber, par, holeDto.Score, holeDto.Putts, holeDto.FairwayHit, holeDto.Penalty);
            if (validation is not null) return ServiceResult<RoundDetailDto>.Failure(validation);
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

        var now = DateTime.UtcNow;
        var round = new Round
        {
            UserId = CurrentUserId,
            CourseId = dto.CourseId,
            CourseTee = tee,
            LegacyTee = RoundRules.IsCustomTee(tee) ? tee.Name : null,
            Date = RoundRules.ToUtc(dto.Date),
            Holes = holes,
            Status = RoundStatus.Completed,
            StartedAt = now,
            CompletedAt = now,
            UpdatedAt = now
        };
        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();
        round.Course = course;
        return ServiceResult<RoundDetailDto>.Success(RoundRules.MapToDetailDto(round));
    }

    public async Task<ServiceResult<RoundDetailDto>> CompleteAsync(int roundId)
    {
        var round = await _context.Rounds
            .Include(r => r.Holes).Include(r => r.Course)
            .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null) return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundNotFound());
        if (round.Status == RoundStatus.Completed)
            return ServiceResult<RoundDetailDto>.Success(RoundRules.MapToDetailDto(round));
        if (round.Status != RoundStatus.Draft)
            return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundInvalid(
                "Only a draft round can be completed."));
        var expected = RoundRules.ExpectedHoleNumbers(round.CourseTee);
        if (round.Holes.Count != expected.Count ||
            !expected.SequenceEqual(round.Holes.Select(h => h.HoleNumber).OrderBy(n => n)))
            return ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundInvalid(
                $"Record all {expected.Count} holes in the selected layout before completing the round."));
        foreach (var hole in round.Holes)
        {
            var validation = RoundRules.ValidateHole(
                hole.HoleNumber, hole.Par, hole.Score, hole.Putts, hole.FairwayHit, hole.Penalty);
            if (validation is not null) return ServiceResult<RoundDetailDto>.Failure(validation);
        }
        round.Status = RoundStatus.Completed;
        round.CompletedAt = DateTime.UtcNow;
        round.UpdatedAt = round.CompletedAt.Value;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            var completed = await _context.Rounds
                .Include(r => r.Holes).Include(r => r.Course)
                .Include(r => r.CourseTee).ThenInclude(t => t!.CourseHoles)
                .FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId &&
                    r.Status == RoundStatus.Completed);
            return completed is not null
                ? ServiceResult<RoundDetailDto>.Success(RoundRules.MapToDetailDto(completed))
                : ServiceResult<RoundDetailDto>.Failure(ServiceErrors.RoundConflict());
        }
        return ServiceResult<RoundDetailDto>.Success(RoundRules.MapToDetailDto(round));
    }

    public async Task<ServiceResult<bool>> AbandonAsync(int roundId)
    {
        var round = await _context.Rounds.FirstOrDefaultAsync(r => r.Id == roundId && r.UserId == CurrentUserId);
        if (round is null) return ServiceResult<bool>.Failure(ServiceErrors.RoundNotFound());
        if (round.Status == RoundStatus.Completed)
            return ServiceResult<bool>.Failure(ServiceErrors.RoundInvalid("A completed round cannot be abandoned."));
        if (round.Status == RoundStatus.Abandoned) return ServiceResult<bool>.Success(true);
        round.Status = RoundStatus.Abandoned;
        round.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            var status = await _context.Rounds.AsNoTracking()
                .Where(r => r.Id == roundId && r.UserId == CurrentUserId)
                .Select(r => (RoundStatus?)r.Status)
                .FirstOrDefaultAsync();
            return status == RoundStatus.Abandoned
                ? ServiceResult<bool>.Success(true)
                : ServiceResult<bool>.Failure(ServiceErrors.RoundConflict());
        }
        return ServiceResult<bool>.Success(true);
    }
}
