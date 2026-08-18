using System.Text.Json.Serialization;

namespace BirdieBuddy.Services.External;

// Shapes mirror https://api.golfcourseapi.com/docs/api - only the fields we
// actually use are mapped; anything else is ignored by System.Text.Json.
//
// AllowReadingFromString is applied to every numeric field: the live API
// sometimes sends numbers as quoted strings (e.g. "id": "34") even though the
// published docs show them unquoted, so we accept both.

public class GolfApiSearchResponse
{
    [JsonPropertyName("courses")]
    public List<GolfApiSearchResult> Courses { get; set; } = new();
}

public class GolfApiSearchResult
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; set; }

    [JsonPropertyName("club_name")]
    public string ClubName { get; set; } = string.Empty;

    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public GolfApiLocation? Location { get; set; }
}

public class GolfApiLocation
{
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
}

public class GolfApiCourseDetail
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; set; }

    [JsonPropertyName("club_name")] public string ClubName { get; set; } = string.Empty;
    [JsonPropertyName("course_name")] public string CourseName { get; set; } = string.Empty;
    [JsonPropertyName("location")] public GolfApiLocation? Location { get; set; }
    [JsonPropertyName("tees")] public GolfApiTees? Tees { get; set; }
}

public class GolfApiTees
{
    [JsonPropertyName("male")] public List<GolfApiTee>? Male { get; set; }
    [JsonPropertyName("female")] public List<GolfApiTee>? Female { get; set; }
}

public class GolfApiTee
{
    [JsonPropertyName("tee_name")] public string TeeName { get; set; } = string.Empty;

    [JsonPropertyName("number_of_holes")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int NumberOfHoles { get; set; }

    [JsonPropertyName("par_total")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ParTotal { get; set; }

    [JsonPropertyName("holes")] public List<GolfApiHole> Holes { get; set; } = new();
}

public class GolfApiHole
{
    [JsonPropertyName("par")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Par { get; set; }

    [JsonPropertyName("yardage")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Yardage { get; set; }

    [JsonPropertyName("handicap")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Handicap { get; set; }
}