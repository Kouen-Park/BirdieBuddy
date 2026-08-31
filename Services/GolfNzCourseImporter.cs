using System.Text.Json;
using System.Text.Json.Serialization;
using BirdieBuddy.Data;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public sealed class GolfNzCourseImporter : IGolfNzCourseImporter
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GolfNzCourseImporter(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<GolfNzImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_environment.ContentRootPath, "scripts", "golf_nz_courses.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(_environment.ContentRootPath, "scripts", "golf_nz_course.json");
        }

        if (!File.Exists(path))
            throw new FileNotFoundException("Golf NZ data file was not found.", path);

        await using var stream = File.OpenRead(path);
        var clubs = await JsonSerializer.DeserializeAsync<List<GolfNzClub>>(stream, JsonOptions, cancellationToken)
            ?? new List<GolfNzClub>();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = new MutableResult();

        foreach (var club in clubs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (club.ClubId <= 0 || string.IsNullOrWhiteSpace(club.ClubName))
                continue;

            var course = await _context.Courses
                .Include(c => c.CourseTees)
                    .ThenInclude(t => t.CourseHoles)
                .FirstOrDefaultAsync(c => c.GolfNzClubId == club.ClubId, cancellationToken);

            // This fallback links the existing seeded Whitford Park record to
            // Golf NZ club 491 instead of inserting a second course.
            course ??= await _context.Courses
                .Include(c => c.CourseTees)
                    .ThenInclude(t => t.CourseHoles)
                .FirstOrDefaultAsync(c => Normalize(c.Name) == Normalize(club.ClubName), cancellationToken);

            if (course is null)
            {
                course = new Course
                {
                    GolfNzClubId = club.ClubId,
                    Name = club.ClubName.Trim(),
                    Location = "New Zealand"
                };
                _context.Courses.Add(course);
                result.CoursesCreated++;
            }
            else
            {
                if (course.GolfNzClubId != club.ClubId)
                    course.GolfNzClubId = club.ClubId;
                if (string.IsNullOrWhiteSpace(course.Location))
                    course.Location = "New Zealand";
                result.CoursesUpdated++;
            }

            var importedTeeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var courseRecord in club.Courses ?? new List<GolfNzCourseRecord>())
            {
                foreach (var marker in courseRecord.Markers ?? new List<GolfNzMarker>())
                {
                    if (string.IsNullOrWhiteSpace(marker.Name))
                        continue;

                    var courseType = courseRecord.Type ?? "";
                    var gender = courseRecord.Gender ?? "";
                    var teeKey = $"{courseType}|{gender}|{courseRecord.NineHoles}|{Normalize(marker.Name)}";
                    if (!importedTeeKeys.Add(teeKey))
                        continue;

                    var tee = course.CourseTees.FirstOrDefault(t =>
                        string.Equals(t.Name, marker.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(t.CourseType, courseType, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(t.Gender, gender, StringComparison.OrdinalIgnoreCase)
                        && t.NineHoles == courseRecord.NineHoles);

                    if (tee is null)
                    {
                        tee = new CourseTee
                        {
                            CourseId = course.Id,
                            Name = marker.Name.Trim(),
                            CourseType = courseType,
                            Gender = gender,
                            NineHoles = courseRecord.NineHoles
                        };
                        course.CourseTees.Add(tee);
                        result.TeesCreated++;
                    }
                    else
                    {
                        result.TeesUpdated++;
                    }

                    tee.Rating = marker.Rating;
                    tee.Slope = marker.Slope;
                    tee.Colour = marker.Colour;
                    tee.TotalPar = marker.TotalPar;
                    tee.FrontNinePar = marker.FrontNinePar;
                    tee.BackNinePar = marker.BackNinePar;
                    tee.FrontNineMetres = marker.FrontNineMetres;
                    tee.BackNineMetres = marker.BackNineMetres;

                    foreach (var holeGroup in (marker.Holes ?? new List<GolfNzHole>())
                                 .Where(h => h.Number is >= 1 and <= 18)
                                 .GroupBy(h => h.Number))
                    {
                        var source = holeGroup
                            .OrderByDescending(h => h.DistanceMetres.HasValue)
                            .ThenByDescending(h => h.StrokeIndex.HasValue)
                            .First();
                        var hole = tee.CourseHoles.FirstOrDefault(h => h.HoleNumber == source.Number);
                        if (hole is null)
                        {
                            hole = new CourseHole
                            {
                                CourseTeeId = tee.Id,
                                HoleNumber = source.Number
                            };
                            tee.CourseHoles.Add(hole);
                            result.HolesCreated++;
                        }
                        else
                        {
                            result.HolesUpdated++;
                        }

                        hole.Par = source.Par;
                        hole.Distance = source.DistanceMetres ?? 0;
                        hole.StrokeIndex = source.StrokeIndex;
                    }
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result.ToImmutable();
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private sealed class MutableResult
    {
        public int CoursesCreated { get; set; }
        public int CoursesUpdated { get; set; }
        public int TeesCreated { get; set; }
        public int TeesUpdated { get; set; }
        public int HolesCreated { get; set; }
        public int HolesUpdated { get; set; }

        public GolfNzImportResult ToImmutable() => new(
            CoursesCreated, CoursesUpdated, TeesCreated, TeesUpdated, HolesCreated, HolesUpdated);
    }
}

internal sealed class GolfNzClub
{
    [JsonPropertyName("club_id")] public int ClubId { get; set; }
    [JsonPropertyName("club_name")] public string ClubName { get; set; } = string.Empty;
    [JsonPropertyName("courses")] public List<GolfNzCourseRecord>? Courses { get; set; }
}

internal sealed class GolfNzCourseRecord
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("gender")] public string? Gender { get; set; }
    [JsonPropertyName("nine_holes")] public bool NineHoles { get; set; }
    [JsonPropertyName("markers")] public List<GolfNzMarker>? Markers { get; set; }
}

internal sealed class GolfNzMarker
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("rating")] public decimal? Rating { get; set; }
    [JsonPropertyName("slope")] public int? Slope { get; set; }
    [JsonPropertyName("colour")] public string? Colour { get; set; }
    [JsonPropertyName("total_par")] public int? TotalPar { get; set; }
    [JsonPropertyName("front_nine_par")] public int? FrontNinePar { get; set; }
    [JsonPropertyName("back_nine_par")] public int? BackNinePar { get; set; }
    [JsonPropertyName("front_nine_metres")] public int? FrontNineMetres { get; set; }
    [JsonPropertyName("back_nine_metres")] public int? BackNineMetres { get; set; }
    [JsonPropertyName("holes")] public List<GolfNzHole>? Holes { get; set; }
}

internal sealed class GolfNzHole
{
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("par")] public int Par { get; set; }
    [JsonPropertyName("stroke_index")] public int? StrokeIndex { get; set; }
    [JsonPropertyName("distance_metres")] public int? DistanceMetres { get; set; }
}
