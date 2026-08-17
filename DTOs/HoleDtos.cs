namespace BirdieBuddy.DTOs;

public record HoleDto(int Id, int HoleNumber, int Par, int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty);

// Par is intentionally absent: the service layer looks it up from the
// course's CourseHole and snapshots it, so a client can never override it.
public record HoleCreateDto(int HoleNumber, int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty);

public record HoleUpdateDto(int Score, int Putts, bool GIR, bool? FairwayHit, int Penalty);
