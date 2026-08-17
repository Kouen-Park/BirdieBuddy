using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface IRoundService
{
    Task<List<RoundSummaryDto>> GetAllAsync();
    Task<RoundDetailDto?> GetByIdAsync(int id);
    Task<(RoundDetailDto? Round, string? Error)> CreateAsync(RoundCreateDto dto);
    Task<bool> UpdateAsync(int id, RoundUpdateDto dto);
    Task<bool> DeleteAsync(int id);

    Task<List<HoleDto>?> GetHolesAsync(int roundId);
    Task<(HoleDto? Hole, string? Error)> AddHoleAsync(int roundId, HoleCreateDto dto);
    Task<(bool Success, string? Error)> UpdateHoleAsync(int roundId, int holeId, HoleUpdateDto dto);
}
