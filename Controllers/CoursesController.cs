using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;
using BirdieBuddy.Infrastructure;

namespace BirdieBuddy.Controllers;

[ApiController]
[Authorize]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly IGolfNzImportJob _golfNzImportJob;
    private readonly IAdminKeyValidator _adminKey;

    public CoursesController(
        ICourseService courseService,
        IGolfNzImportJob golfNzImportJob,
        IAdminKeyValidator adminKey)
    {
        _courseService = courseService;
        _golfNzImportJob = golfNzImportJob;
        _adminKey = adminKey;
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
            return Problem(detail: ex.Message, statusCode: 400, title: "Course could not be created.");
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
            return Problem(detail: error, statusCode: error == "Course not found." ? 404 : 409, title: "Course could not be deleted.");

        return NoContent();
    }

    [HttpPost("import-golf-nz")]
    public ActionResult<GolfNzImportJobStatus> StartGolfNzImport()
    {
        if (!_adminKey.IsValid(Request)) return NotFound();
        var started = _golfNzImportJob.TryStart(out var status);
        return started ? Accepted(status) : Ok(status);
    }

    [HttpGet("import-golf-nz/status")]
    public ActionResult<GolfNzImportJobStatus> GetGolfNzImportStatus()
    {
        if (!_adminKey.IsValid(Request)) return NotFound();
        return Ok(_golfNzImportJob.GetStatus());
    }

}
