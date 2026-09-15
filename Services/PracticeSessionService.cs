using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public sealed class PracticeSessionService : IPracticeSessionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    public PracticeSessionService(ApplicationDbContext context, ICurrentUser currentUser)
    { _context = context; _currentUser = currentUser; }

    private int UserId => _currentUser.Id ?? 0;

    public async Task<List<PracticeSessionDto>> GetRecentAsync(int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);
        return await _context.PracticeSessions.AsNoTracking()
            .Where(s => s.UserId == UserId).OrderByDescending(s => s.StartedAt).Take(limit)
            .Select(s => new PracticeSessionDto(s.Id, s.FocusCode, s.DrillTitle, s.Minutes,
                s.StartedAt, s.CompletedAt, s.Result, s.Notes)).ToListAsync();
    }

    public async Task<ServiceResult<PracticeSessionDto>> StartAsync(PracticeSessionCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FocusCode) || string.IsNullOrWhiteSpace(dto.DrillTitle))
            return ServiceResult<PracticeSessionDto>.Failure(ServiceErrors.PracticeInvalid(
                "A practice focus and drill title are required."));
        if (dto.Minutes is < 1 or > 240)
            return ServiceResult<PracticeSessionDto>.Failure(ServiceErrors.PracticeInvalid(
                "Practice minutes must be between 1 and 240."));
        var session = new PracticeSession { UserId = UserId, FocusCode = dto.FocusCode.Trim(), DrillTitle = dto.DrillTitle.Trim(), Minutes = dto.Minutes };
        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();
        return ServiceResult<PracticeSessionDto>.Success(ToDto(session));
    }

    public async Task<ServiceResult<PracticeSessionDto>> CompleteAsync(int id, PracticeSessionCompleteDto dto)
    {
        var session = await _context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == UserId);
        if (session is null)
            return ServiceResult<PracticeSessionDto>.Failure(ServiceErrors.PracticeNotFound());
        if (session.CompletedAt.HasValue) return ServiceResult<PracticeSessionDto>.Success(ToDto(session));
        if (dto.Result?.Length > 500 || dto.Notes?.Length > 2000)
            return ServiceResult<PracticeSessionDto>.Failure(ServiceErrors.PracticeInvalid(
                "Practice result or notes are too long."));
        session.Result = string.IsNullOrWhiteSpace(dto.Result) ? null : dto.Result.Trim();
        session.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        session.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ServiceResult<PracticeSessionDto>.Success(ToDto(session));
    }

    private static PracticeSessionDto ToDto(PracticeSession s) => new(s.Id, s.FocusCode, s.DrillTitle, s.Minutes, s.StartedAt, s.CompletedAt, s.Result, s.Notes);
}
