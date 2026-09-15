using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface IPracticeSessionService
{
    Task<List<PracticeSessionDto>> GetRecentAsync(int limit = 10);
    Task<ServiceResult<PracticeSessionDto>> StartAsync(PracticeSessionCreateDto dto);
    Task<ServiceResult<PracticeSessionDto>> CompleteAsync(int id, PracticeSessionCompleteDto dto);
}
