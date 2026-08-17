using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public class CourseService : ICourseService
{
    private readonly ApplicationDbContext _context;

    public CourseService(ApplicationDbContext context)
    {
        _context = context;
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

    private static CourseDto MapToDto(Course course)
    {
        var holes = course.CourseHoles
            .OrderBy(h => h.HoleNumber)
            .Select(h => new CourseHoleDto(h.Id, h.HoleNumber, h.Par, h.Distance))
            .ToList();

        return new CourseDto(course.Id, course.Name, course.Location, holes);
    }
}
