using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using BirdieBuddy.Services.External;

namespace BirdieBuddy.Services;

public class CourseService : ICourseService
{
    private readonly ApplicationDbContext _context;
    private readonly IGolfCourseApiClient _golfApiClient;

    public CourseService(ApplicationDbContext context, IGolfCourseApiClient golfApiClient)
    {
        _context = context;
        _golfApiClient = golfApiClient;
    }

    public async Task<List<CourseSummaryDto>> GetAllAsync()
    {
        return await _context.Courses
            .OrderBy(c => c.Name)
            .Select(c => new CourseSummaryDto(c.Id, c.Name, c.Location))
            .ToListAsync();
    }

    public async Task<CourseDto?> GetByIdAsync(int id)
    {
        var course = await _context.Courses
            .Include(c => c.CourseHoles)
            .FirstOrDefaultAsync(c => c.Id == id);

        return course is null ? null : MapToDto(course);
    }

    public async Task<CourseDto> CreateAsync(CourseCreateDto dto)
    {
        if (dto.Holes.Count != 18)
            throw new ArgumentException("A course must have exactly 18 holes.");

        if (dto.Holes.Select(h => h.HoleNumber).Distinct().Count() != 18 ||
            dto.Holes.Any(h => h.HoleNumber is < 1 or > 18))
            throw new ArgumentException("Hole numbers must be unique and between 1 and 18.");

        var course = new Course
        {
            Name = dto.Name,
            Location = dto.Location,
            CourseHoles = dto.Holes.Select(h => new CourseHole
            {
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Distance = h.Distance
            }).ToList()
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        return MapToDto(course);
    }

    public async Task<bool> UpdateAsync(int id, CourseUpdateDto dto)
    {
        var course = await _context.Courses.FindAsync(id);
        if (course is null) return false;

        course.Name = dto.Name;
        course.Location = dto.Location;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var course = await _context.Courses.FindAsync(id);
        if (course is null) return (false, "Course not found.");

        // A Round -> Course delete is Restrict at the DB level, but we check
        // here first so the API can return a clear 409 instead of a raw SQL error.
        bool hasRounds = await _context.Rounds.AnyAsync(r => r.CourseId == id);
        if (hasRounds)
            return (false, "Cannot delete a course that has recorded rounds.");

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<ExternalCourseSummaryDto>> SearchExternalAsync(string query)
    {
        var results = await _golfApiClient.SearchAsync(query);

        return results.Select(r => new ExternalCourseSummaryDto(
            r.Id,
            r.ClubName,
            r.CourseName,
            FormatLocation(r.Location),
            r.Tees?.Male ?? 0,
            r.Tees?.Female ?? 0
        )).ToList();
    }

    public async Task<(CourseDto? Course, string? Error)> ImportExternalAsync(string externalId, string? preferredTeeName)
    {
        var detail = await _golfApiClient.GetCourseAsync(externalId);
        if (detail is null)
            return (null, "That course couldn't be found on GolfCourseAPI.");

        var candidateTees = (detail.Tees?.Male ?? new List<GolfApiTee>())
            .Concat(detail.Tees?.Female ?? new List<GolfApiTee>())
            .ToList();

        var tee = !string.IsNullOrWhiteSpace(preferredTeeName)
            ? candidateTees.FirstOrDefault(t => string.Equals(t.TeeName, preferredTeeName, StringComparison.OrdinalIgnoreCase))
            : null;
        tee ??= candidateTees.FirstOrDefault();

        // Some API records contain only course metadata, or a tee without
        // detailed holes. Import those records as hole-less courses so the user
        // can enter the scorecard manually later.
        var hasDetailedHoles = tee is not null && tee.Holes.Count > 0;
        if (hasDetailedHoles && tee!.Holes.Count != 18)
        {
            return (null, $"The '{tee.TeeName}' tee only has data for {tee.Holes.Count} holes - Birdie Buddy currently requires a full 18-hole course when detailed hole data is available.");
        }

        var name = string.IsNullOrWhiteSpace(detail.CourseName) || detail.CourseName == detail.ClubName
            ? detail.ClubName
            : $"{detail.ClubName} - {detail.CourseName}";

        if (await _context.Courses.AnyAsync(c => c.Name == name))
            return (null, $"A course named \"{name}\" already exists.");

        var course = new Course
        {
            Name = name,
            Location = FormatLocation(detail.Location) ?? "",
            CourseHoles = hasDetailedHoles
                ? tee!.Holes.Select((h, i) => new CourseHole
                {
                    HoleNumber = i + 1,
                    Par = h.Par,
                    Distance = (int)Math.Round(h.Yardage * 0.9144) // yards -> metres
                }).ToList()
                : new List<CourseHole>()
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        return (MapToDto(course), null);
    }

    private static string? FormatLocation(GolfApiLocation? location)
    {
        if (location is null) return null;

        var parts = new[] { location.City, location.State, location.Country }
            .Where(s => !string.IsNullOrWhiteSpace(s));

        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? location.Address : joined;
    }

    private static CourseDto MapToDto(Course course)
    {
        var holes = course.CourseHoles
            .OrderBy(h => h.HoleNumber)
            .Select(h => new CourseHoleDto(h.Id, h.HoleNumber, h.Par, h.Distance))
            .ToList();

        return new CourseDto(course.Id, course.Name, course.Location, holes);
    }
}
