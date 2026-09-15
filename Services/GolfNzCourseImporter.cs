using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
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

    public async Task<GolfNzImportResult> ImportAsync(long importRunId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_environment.ContentRootPath, "scripts", "golf_nz_courses.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(_environment.ContentRootPath, "scripts", "golf_nz_course.json");
        }

        if (!File.Exists(path))
            throw new FileNotFoundException("Golf NZ data file was not found.", path);

        var run = await _context.GolfNzImportRuns.FindAsync(new object[] { importRunId }, cancellationToken)
            ?? throw new InvalidOperationException("The Golf NZ import run was not found.");
        var sourceBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        run.SourceVersion = $"sha256:{Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant()}";
        await _context.SaveChangesAsync(cancellationToken);
        await using var stream = new MemoryStream(sourceBytes, writable: false);
        var clubs = await JsonSerializer.DeserializeAsync<List<GolfNzClub>>(stream, JsonOptions, cancellationToken)
            ?? new List<GolfNzClub>();
        if (clubs.Count == 0)
            throw new InvalidDataException("Golf NZ source contains no clubs; existing data was not changed.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = new MutableResult();
        var seenTeeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenHoleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingCourses = await _context.Courses
            .Include(c => c.CourseTees)
                .ThenInclude(t => t.CourseHoles)
            .ToListAsync(cancellationToken);

        foreach (var club in clubs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (club.ClubId <= 0 || string.IsNullOrWhiteSpace(club.ClubName))
                continue;

            var course = existingCourses.FirstOrDefault(c => c.GolfNzClubId == club.ClubId);

            // This in-memory fallback links the existing seeded Whitford Park
            // record to Golf NZ club 491 instead of inserting a second course.
            course ??= existingCourses.FirstOrDefault(c =>
                Normalize(c.Name) == Normalize(club.ClubName));

            if (course is null)
            {
                course = new Course
                {
                    GolfNzClubId = club.ClubId,
                    Name = club.ClubName.Trim(),
                    Location = "New Zealand"
                };
                _context.Courses.Add(course);
                existingCourses.Add(course);
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
                    var sourceKey = $"{club.ClubId}|{teeKey}";
                    seenTeeKeys.Add(sourceKey);

                    var tee = course.CourseTees.FirstOrDefault(t =>
                        string.Equals(t.Name, marker.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(t.CourseType, courseType, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(t.Gender, gender, StringComparison.OrdinalIgnoreCase)
                        && t.NineHoles == courseRecord.NineHoles);

                    if (tee is null)
                    {
                        tee = new CourseTee
                        {
                            Course = course,
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
                    tee.SourceKey = sourceKey;
                    tee.IsActive = true;
                    tee.LastSeenImportRunId = run.Id;
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
                        var holeSourceKey = $"{sourceKey}|{source.Number}";
                        seenHoleKeys.Add(holeSourceKey);
                        if (hole is null)
                        {
                            hole = new CourseHole
                            {
                                CourseTee = tee,
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
                        hole.SourceKey = holeSourceKey;
                        hole.IsActive = true;
                        hole.LastSeenImportRunId = run.Id;
                    }
                }
            }
        }

        var staleTees = await _context.CourseTees
            .Where(t => t.SourceKey != null && !seenTeeKeys.Contains(t.SourceKey))
            .ToListAsync(cancellationToken);
        foreach (var tee in staleTees)
        {
            if (tee.IsActive) { tee.IsActive = false; result.RecordsDeactivated++; }
            tee.LastSeenImportRunId = run.Id;
        }
        var staleHoles = await _context.CourseHoles
            .Where(h => h.SourceKey != null && !seenHoleKeys.Contains(h.SourceKey))
            .ToListAsync(cancellationToken);
        foreach (var hole in staleHoles)
        {
            if (hole.IsActive) { hole.IsActive = false; result.RecordsDeactivated++; }
            hole.LastSeenImportRunId = run.Id;
        }

        await _context.SaveChangesAsync(cancellationToken);
        run.Status = GolfNzImportStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        run.CoursesCreated = result.CoursesCreated;
        run.CoursesUpdated = result.CoursesUpdated;
        run.TeesCreated = result.TeesCreated;
        run.TeesUpdated = result.TeesUpdated;
        run.HolesCreated = result.HolesCreated;
        run.HolesUpdated = result.HolesUpdated;
        run.RecordsDeactivated = result.RecordsDeactivated;
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
        public int RecordsDeactivated { get; set; }

        public GolfNzImportResult ToImmutable() => new(
            CoursesCreated, CoursesUpdated, TeesCreated, TeesUpdated, HolesCreated, HolesUpdated, RecordsDeactivated);
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
