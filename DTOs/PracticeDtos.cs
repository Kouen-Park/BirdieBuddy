namespace BirdieBuddy.DTOs;

public record PracticeSessionDto(int Id, string FocusCode, string DrillTitle, int Minutes,
    DateTime StartedAt, DateTime? CompletedAt, string? Result, string? Notes);

public record PracticeSessionCreateDto(string FocusCode, string DrillTitle, int Minutes);

public record PracticeSessionCompleteDto(string? Result, string? Notes);
