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

    public async Task<(PracticeSessionDto? Session, string? Error)> StartAsync(PracticeSessionCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FocusCode) || string.IsNullOrWhiteSpace(dto.DrillTitle))
            return (null, "A practice focus and drill title are required.");
        if (dto.Minutes is < 1 or > 240) return (null, "Practice minutes must be between 1 and 240.");
        var session = new PracticeSession { UserId = UserId, FocusCode = dto.FocusCode.Trim(), DrillTitle = dto.DrillTitle.Trim(), Minutes = dto.Minutes };
        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();
        return (ToDto(session), null);
    }

    public async Task<(PracticeSessionDto? Session, string? Error)> CompleteAsync(int id, PracticeSessionCompleteDto dto)
    {
        var session = await _context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == UserId);
        if (session is null) return (null, "Practice session not found.");
        if (session.CompletedAt.HasValue) return (ToDto(session), null);
        if (dto.Result?.Length > 500 || dto.Notes?.Length > 2000) return (null, "Practice result or notes are too long.");
        session.Result = string.IsNullOrWhiteSpace(dto.Result) ? null : dto.Result.Trim();
        session.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        session.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (ToDto(session), null);
    }

    private static PracticeSessionDto ToDto(PracticeSession s) => new(s.Id, s.FocusCode, s.DrillTitle, s.Minutes, s.StartedAt, s.CompletedAt, s.Result, s.Notes);
}
