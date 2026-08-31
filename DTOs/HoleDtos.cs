namespace BirdieBuddy.DTOs;

public record HoleDto(int Id, int HoleNumber, int Par, int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty);

// Par is optional for normal courses (the service snapshots the course hole's
// value), but is supplied by the client for imported courses without hole data.
public record HoleCreateDto(int HoleNumber, int? Par, int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty);

public record HoleUpdateDto(int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty);
