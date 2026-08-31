namespace BirdieBuddy.Services;

public interface IGolfNzImportJob
{
    bool TryStart(out GolfNzImportJobStatus status);
    GolfNzImportJobStatus GetStatus();
}

public sealed record GolfNzImportJobStatus(
    string State,
    GolfNzImportResult? Result,
    string? Error);
