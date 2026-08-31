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

    // Proxies OpenGolfAPI so external API details stay on the server.
    [HttpGet("external/search")]
    public async Task<ActionResult<List<ExternalCourseSummaryDto>>> SearchExternal([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        try
        {
            return Ok(await _courseService.SearchExternalAsync(q));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = $"Couldn't reach OpenGolfAPI: {ex.Message}" });
        }
        catch (Exception ex)
        {
            // Surfaced with detail temporarily to make first-run debugging easier;
            // narrow this back down once the external API integration is confirmed working.
            return StatusCode(500, new { error = $"Unexpected error calling OpenGolfAPI: {ex.Message}" });
        }
    }

    [HttpPost("external/{externalId}/import")]
    public async Task<ActionResult<CourseDto>> ImportExternal(string externalId, [FromQuery] string? tee)
    {
        try
        {
            var (course, error) = await _courseService.ImportExternalAsync(externalId, tee);
            if (error is not null) return BadRequest(new { error });
            return CreatedAtAction(nameof(GetById), new { id = course!.Id }, course);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = $"Couldn't reach OpenGolfAPI: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Unexpected error importing course: {ex.Message}" });
        }
    }
}
