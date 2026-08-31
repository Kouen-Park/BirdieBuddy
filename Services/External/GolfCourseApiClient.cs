using System.Text.Json;

namespace BirdieBuddy.Services.External;

public class GolfCourseApiClient : IGolfCourseApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GolfCourseApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<GolfApiSearchResult>> SearchAsync(string query)
    {
        var response = await _http.GetAsync($"v1/courses/search?q={Uri.EscapeDataString(query)}");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<GolfApiSearchResponse>(stream, JsonOptions);
        return result?.Courses ?? new List<GolfApiSearchResult>();
    }

    public async Task<GolfApiCourseDetail?> GetCourseAsync(string externalId)
    {
        var response = await _http.GetAsync($"v1/courses/{Uri.EscapeDataString(externalId)}");
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<GolfApiCourseDetail>(stream, JsonOptions);
    }
}
