using System.Text.Json.Serialization;

namespace BirdieBuddy.Services.External;

// OpenGolfAPI exposes keyless read endpoints. Search returns compact course
// records; detail returns a scorecard with hole/par pairs and no tee grouping.

public class GolfApiSearchResponse
{
    [JsonPropertyName("courses")]
    public List<GolfApiSearchResult> Courses { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class GolfApiSearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string ClubName { get; set; } = string.Empty;

    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("type")]
    public string? CourseType { get; set; }

    [JsonPropertyName("par")]
    public int? ParTotal { get; set; }
}

public class GolfApiCourseDetail
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string ClubName { get; set; } = string.Empty;

    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("par")]
    public int? ParTotal { get; set; }

    [JsonPropertyName("scorecard")]
    public List<GolfApiScorecardHole> Scorecard { get; set; } = new();
}

public class GolfApiScorecardHole
{
    [JsonPropertyName("hole")]
    public int Hole { get; set; }

    [JsonPropertyName("par")]
    public int Par { get; set; }
}
