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
        return course is null ? this.ApiProblem(404, "course.not_found", "Course not found.") : Ok(course);
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
            return this.ApiProblem(400, "course.invalid", "Course could not be created.", ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CourseUpdateDto dto)
    {
        var success = await _courseService.UpdateAsync(id, dto);
        return success ? NoContent() : this.ApiProblem(404, "course.not_found", "Course not found.");
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _courseService.DeleteAsync(id);
        if (!success)
            return this.ApiProblem(error == "Course not found." ? 404 : 409,
                error == "Course not found." ? "course.not_found" : "course.in_use",
                "Course could not be deleted.", error);

        return NoContent();
    }

    [HttpPost("import-golf-nz")]
    public ActionResult<GolfNzImportJobStatus> StartGolfNzImport()
    {
        if (!_adminKey.IsValid(Request)) return this.ApiProblem(404, "admin.not_found", "Resource not found.");
        var started = _golfNzImportJob.TryStart(out var status);
        return started ? Accepted(status) : Ok(status);
    }

    [HttpGet("import-golf-nz/status")]
    public ActionResult<GolfNzImportJobStatus> GetGolfNzImportStatus()
    {
        if (!_adminKey.IsValid(Request)) return this.ApiProblem(404, "admin.not_found", "Resource not found.");
        return Ok(_golfNzImportJob.GetStatus());
    }

}
