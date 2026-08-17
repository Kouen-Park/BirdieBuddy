using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;

namespace BirdieBuddy.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CourseSummaryDto>>> GetAll()
        => Ok(await _courseService.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourseDto>> GetById(int id)
    {
        var course = await _courseService.GetByIdAsync(id);
        return course is null ? NotFound() : Ok(course);
    }

    [HttpPost]
    public async Task<ActionResult<CourseDto>> Create(CourseCreateDto dto)
    {
        try
        {
            var course = await _courseService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = course.Id }, course);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CourseUpdateDto dto)
    {
        var success = await _courseService.UpdateAsync(id, dto);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _courseService.DeleteAsync(id);
        if (!success)
            return error == "Course not found." ? NotFound(new { error }) : Conflict(new { error });

        return NoContent();
    }
}
