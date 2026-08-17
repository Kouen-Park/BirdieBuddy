namespace BirdieBuddy.DTOs;

public record RoundSummaryDto(int Id, int CourseId, string CourseName, DateTime Date, string Tee, int TotalScore, int ScoreToPar);

public record RoundDetailDto(int Id, int CourseId, string CourseName, DateTime Date, string Tee, List<HoleDto> Holes);

// Holes are submitted together with the round: the Add Round page fills out
// the whole scorecard and saves it in one action. Individual holes can still
// be added/edited afterwards via the /holes sub-resource endpoints.
public record RoundCreateDto(int CourseId, DateTime Date, string Tee, List<HoleCreateDto> Holes);

public record RoundUpdateDto(DateTime Date, string Tee);
