using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

public interface IRoundQueryService
{
    Task<List<RoundSummaryDto>> GetAllAsync();
    Task<List<RoundOptionDto>> GetOptionsAsync(int limit = 100);
    Task<RoundPageDto> GetPageAsync(RoundQueryDto query);
    Task<RoundDetailDto?> GetByIdAsync(int id);
    Task<List<HoleDto>?> GetHolesAsync(int roundId);
}

public interface ILiveRoundService
{
    Task<ServiceResult<RoundDetailDto>> StartAsync(RoundStartDto dto);
    Task<ServiceResult<HoleDto>> UpsertHoleAsync(int roundId, int holeNumber, HoleUpsertDto dto);
    Task<ServiceResult<HoleDto>> AddHoleAsync(int roundId, HoleCreateDto dto);
}

public interface IRoundLifecycleService
{
    Task<ServiceResult<RoundDetailDto>> CreateAsync(RoundCreateDto dto);
    Task<ServiceResult<RoundDetailDto>> CompleteAsync(int roundId);
    Task<ServiceResult<bool>> AbandonAsync(int roundId);
}

public interface ICompletedRoundEditor
{
    Task<ServiceResult<bool>> UpdateAsync(int id, RoundUpdateDto dto);
    Task<ServiceResult<bool>> DeleteAsync(int id);
    Task<ServiceResult<bool>> UpdateHoleAsync(int roundId, int holeId, HoleUpdateDto dto);
}
