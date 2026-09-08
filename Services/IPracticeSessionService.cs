using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface IPracticeSessionService
{
    Task<List<PracticeSessionDto>> GetRecentAsync(int limit = 10);
    Task<(PracticeSessionDto? Session, string? Error)> StartAsync(PracticeSessionCreateDto dto);
    Task<(PracticeSessionDto? Session, string? Error)> CompleteAsync(int id, PracticeSessionCompleteDto dto);
}
