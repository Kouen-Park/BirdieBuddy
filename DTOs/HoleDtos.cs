using System.ComponentModel.DataAnnotations;

namespace BirdieBuddy.DTOs;

public record HoleDto(int Id, int HoleNumber, int Par, int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty);

// Par is optional for normal courses (the service snapshots the course hole's
// value), but is supplied by the client for imported courses without hole data.
public record HoleCreateDto(
    [param: Range(1, 18)] int HoleNumber,
    [param: Range(3, 6)] int? Par,
    [param: Range(1, 20)] int Score,
    [param: Range(0, 10)] int Putts,
    bool GIR,
    bool? FairwayHit,
    [param: Range(0, 20)] int Penalty);

public record HoleUpdateDto(
    [param: Range(1, 20)] int Score,
    [param: Range(0, 10)] int Putts,
    bool GIR,
    bool? FairwayHit,
    [param: Range(0, 20)] int Penalty,
    bool CheckExpected = false, HoleDto? ExpectedHole = null);
