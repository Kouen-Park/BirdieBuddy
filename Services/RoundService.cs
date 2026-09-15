using BirdieBuddy.Data;
using BirdieBuddy.DTOs;

namespace BirdieBuddy.Services;

// Compatibility facade for controllers and existing callers. Feature services
// own the query, live-entry, lifecycle, and completed-round editing workflows.
public sealed class RoundService : IRoundService
{
    public const string ConflictMessage =
        "This round changed in another session. Review the server record before retrying.";

    private readonly IRoundQueryService _queries;
    private readonly ILiveRoundService _liveRounds;
    private readonly IRoundLifecycleService _lifecycle;
    private readonly ICompletedRoundEditor _editor;

    public RoundService(
        IRoundQueryService queries,
        ILiveRoundService liveRounds,
        IRoundLifecycleService lifecycle,
        ICompletedRoundEditor editor)
    {
        _queries = queries;
        _liveRounds = liveRounds;
        _lifecycle = lifecycle;
        _editor = editor;
    }

    // Keeps service-level tests and small host integrations source-compatible.
    public RoundService(ApplicationDbContext context, ICurrentUser currentUser)
        : this(
            new RoundQueryService(context, currentUser),
            new LiveRoundService(context, currentUser),
            new RoundLifecycleService(context, currentUser),
            new CompletedRoundEditor(context, currentUser))
    {
    }

    public Task<List<RoundSummaryDto>> GetAllAsync() => _queries.GetAllAsync();
    public Task<List<RoundOptionDto>> GetOptionsAsync(int limit = 100) => _queries.GetOptionsAsync(limit);
    public Task<RoundPageDto> GetPageAsync(RoundQueryDto query) => _queries.GetPageAsync(query);
    public Task<RoundDetailDto?> GetByIdAsync(int id) => _queries.GetByIdAsync(id);
    public Task<List<HoleDto>?> GetHolesAsync(int roundId) => _queries.GetHolesAsync(roundId);

    public Task<ServiceResult<RoundDetailDto>> StartAsync(RoundStartDto dto) => _liveRounds.StartAsync(dto);
    public Task<ServiceResult<HoleDto>> UpsertHoleAsync(int roundId, int holeNumber, HoleUpsertDto dto) =>
        _liveRounds.UpsertHoleAsync(roundId, holeNumber, dto);
    public Task<ServiceResult<HoleDto>> AddHoleAsync(int roundId, HoleCreateDto dto) =>
        _liveRounds.AddHoleAsync(roundId, dto);

    public Task<ServiceResult<RoundDetailDto>> CreateAsync(RoundCreateDto dto) => _lifecycle.CreateAsync(dto);
    public Task<ServiceResult<RoundDetailDto>> CompleteAsync(int roundId) => _lifecycle.CompleteAsync(roundId);
    public Task<ServiceResult<bool>> AbandonAsync(int roundId) => _lifecycle.AbandonAsync(roundId);

    public async Task<bool> UpdateAsync(int id, RoundUpdateDto dto) =>
        (await _editor.UpdateAsync(id, dto)).IsSuccess;
    public Task<ServiceResult<bool>> UpdateWithErrorAsync(int id, RoundUpdateDto dto) => _editor.UpdateAsync(id, dto);
    public Task<ServiceResult<bool>> DeleteAsync(int id) => _editor.DeleteAsync(id);
    public Task<ServiceResult<bool>> UpdateHoleAsync(int roundId, int holeId, HoleUpdateDto dto) =>
        _editor.UpdateHoleAsync(roundId, holeId, dto);
}
