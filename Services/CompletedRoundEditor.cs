using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public sealed class CompletedRoundEditor : ICompletedRoundEditor
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public CompletedRoundEditor(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<ServiceResult<bool>> UpdateAsync(int id, RoundUpdateDto dto)
    {
        var round = await _context.Rounds
            .Where(r => r.Id == id && r.UserId == CurrentUserId)
            .Include(r => r.Course).ThenInclude(c => c!.CourseTees)
            .FirstOrDefaultAsync();
        if (round is null || round.Course is null || round.Status == RoundStatus.Abandoned || dto.Date == default)
            return ServiceResult<bool>.Failure(ServiceErrors.RoundNotFound());
        if (dto.ExpectedUpdatedAt.HasValue && round.UpdatedAt != dto.ExpectedUpdatedAt.Value)
            return ServiceResult<bool>.Failure(ServiceErrors.RoundConflict());

        var teeResult = RoundRules.ResolveTee(round.Course, dto.CourseTeeId, dto.Tee, true);
        if (!teeResult.IsSuccess) return ServiceResult<bool>.Failure(teeResult.Error!);
        var tee = teeResult.Value!;
        if (round.CourseTeeId != tee.Id && await _context.Holes.AnyAsync(h => h.RoundId == id))
            return ServiceResult<bool>.Failure(ServiceErrors.RoundInvalid(
                "A round with recorded holes cannot change tees."));

        round.CourseTee = tee;
        round.LegacyTee = RoundRules.IsCustomTee(tee) ? tee.Name : null;
        round.Date = RoundRules.ToUtc(dto.Date);
        round.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<bool>.Failure(ServiceErrors.RoundConflict());
        }
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var round = await _context.Rounds.FirstOrDefaultAsync(r => r.Id == id && r.UserId == CurrentUserId);
        if (round is null) return ServiceResult<bool>.Failure(ServiceErrors.RoundNotFound());
        _context.Rounds.Remove(round);
        await _context.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> UpdateHoleAsync(int roundId, int holeId, HoleUpdateDto dto)
    {
        var hole = await _context.Holes
            .Include(h => h.Round)
            .FirstOrDefaultAsync(h => h.Id == holeId && h.RoundId == roundId && h.Round!.UserId == CurrentUserId);
        if (hole is null) return ServiceResult<bool>.Failure(ServiceErrors.HoleNotFound());
        if (hole.Round!.Status != RoundStatus.Completed)
            return ServiceResult<bool>.Failure(ServiceErrors.HoleInvalid(
                "Use live entry for draft rounds; abandoned rounds cannot be edited."));
        var validation = RoundRules.ValidateHole(
            hole.HoleNumber, hole.Par, dto.Score, dto.Putts, dto.FairwayHit, dto.Penalty);
        if (validation is not null) return ServiceResult<bool>.Failure(validation);

        if (dto.CheckExpected && dto.ExpectedHole != RoundRules.MapToHoleDto(hole))
        {
            if (hole.Score == dto.Score && hole.Putts == dto.Putts && hole.GIR == dto.GIR &&
                hole.FairwayHit == dto.FairwayHit && hole.Penalty == dto.Penalty)
                return ServiceResult<bool>.Success(true);
            return ServiceResult<bool>.Failure(ServiceErrors.RoundConflict());
        }
        hole.Score = dto.Score;
        hole.Putts = dto.Putts;
        hole.GIR = dto.GIR;
        hole.FairwayHit = dto.FairwayHit;
        hole.Penalty = dto.Penalty;
        hole.Round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }
}
