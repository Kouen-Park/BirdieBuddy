namespace BirdieBuddy.Services.External;

public interface IGolfCourseApiClient
{
    Task<List<GolfApiSearchResult>> SearchAsync(string query);
    Task<GolfApiCourseDetail?> GetCourseAsync(string externalId);
}
