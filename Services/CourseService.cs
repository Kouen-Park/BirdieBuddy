using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public class CourseService : ICourseService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public CourseService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.Id ?? 0;

    public async Task<List<CourseSummaryDto>> GetAllAsync()
    {
        return await _context.Courses
            .Where(c => c.UserId == null || c.UserId == CurrentUserId)
            .OrderBy(c => c.Name)
            .Select(c => new CourseSummaryDto(c.Id, c.Name, c.Location))
            .ToListAsync();
    }

    public async Task<CourseDto?> GetByIdAsync(int id)
    {
        var course = await _context.Courses
            .Where(c => c.UserId == null || c.UserId == CurrentUserId)
            .Include(c => c.CourseTees)
                .ThenInclude(t => t.CourseHoles)
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

        var tee = new CourseTee
        {
            Name = "Default",
            CourseType = "CUSTOM",
            Gender = "",
            NineHoles = false,
            CourseHoles = dto.Holes.Select(h => new CourseHole
            {
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Distance = h.Distance
            }).ToList()
        };

        var course = new Course
        {
            UserId = CurrentUserId,
            Name = dto.Name,
            Location = dto.Location,
            CourseTees = new List<CourseTee> { tee }
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        return MapToDto(course);
    }

    public async Task<bool> UpdateAsync(int id, CourseUpdateDto dto)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
        if (course is null) return false;

        course.Name = dto.Name;
        course.Location = dto.Location;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
        if (course is null) return (false, "Course not found.");

        bool hasRounds = await _context.Rounds.AnyAsync(r => r.CourseId == id && r.UserId == CurrentUserId);
        if (hasRounds)
            return (false, "Cannot delete a course that has recorded rounds.");

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    private static CourseDto MapToDto(Course course)
    {
        var tees = course.CourseTees
            .OrderBy(t => t.NineHoles)
            .ThenBy(t => t.Gender)
            .ThenBy(t => t.Name)
            .Select(MapToTeeDto)
            .ToList();

        // Keep the old flat Holes property populated with the first tee for
        // clients that have not yet adopted the CourseTee response.
        var firstHoles = tees.FirstOrDefault()?.Holes ?? new List<CourseHoleDto>();
        return new CourseDto(course.Id, course.GolfNzClubId, course.Name, course.Location, tees, firstHoles);
    }

    private static CourseTeeDto MapToTeeDto(CourseTee tee)
    {
        var holes = tee.CourseHoles
            .OrderBy(h => h.HoleNumber)
            .Select(h => new CourseHoleDto(h.Id, h.HoleNumber, h.Par, h.Distance, h.StrokeIndex))
            .ToList();

        return new CourseTeeDto(
            tee.Id,
            tee.Name,
            tee.CourseType,
            tee.Gender,
            tee.NineHoles,
            tee.Rating,
            tee.Slope,
            tee.Colour,
            tee.TotalPar,
            tee.FrontNinePar,
            tee.BackNinePar,
            tee.FrontNineMetres,
            tee.BackNineMetres,
            holes);
    }
}
