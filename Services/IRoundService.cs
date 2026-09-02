using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface IRoundService
{
    Task<List<RoundSummaryDto>> GetAllAsync();
    Task<RoundPageDto> GetPageAsync(RoundQueryDto query);
    Task<RoundDetailDto?> GetByIdAsync(int id);
    Task<(RoundDetailDto? Round, string? Error)> CreateAsync(RoundCreateDto dto);
    Task<bool> UpdateAsync(int id, RoundUpdateDto dto);
    Task<bool> DeleteAsync(int id);

    Task<List<HoleDto>?> GetHolesAsync(int roundId);
    Task<(HoleDto? Hole, string? Error)> AddHoleAsync(int roundId, HoleCreateDto dto);
    Task<(bool Success, string? Error)> UpdateHoleAsync(int roundId, int holeId, HoleUpdateDto dto);
    Task<(RoundDetailDto? Round, string? Error)> StartAsync(RoundStartDto dto);
    Task<(HoleDto? Hole, string? Error)> UpsertHoleAsync(int roundId, int holeNumber, HoleUpsertDto dto);
    Task<(RoundDetailDto? Round, string? Error)> CompleteAsync(int roundId);
    Task<bool> AbandonAsync(int roundId);
}
