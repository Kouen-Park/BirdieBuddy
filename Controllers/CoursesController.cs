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
    [AllowAnonymous]
    public async Task<ActionResult<List<CourseSummaryDto>>> GetAll()
        => Ok(await _courseService.GetAllAsync());

    [HttpGet("page")]
    [AllowAnonymous]
    public async Task<ActionResult<CoursePageDto>> GetPage([FromQuery] CourseQueryDto query)
        => Ok(await _courseService.GetPageAsync(query));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<CourseDto>> GetById(int id)
    {
        var course = await _courseService.GetByIdAsync(id);
        return course is null ? this.ApiProblem(404, "course.not_found", "Course not found.") : Ok(course);
    }

    [HttpPost]
    public async Task<ActionResult<CourseDto>> Create(CourseCreateDto dto)
    {
        var result = await _courseService.CreateAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ApiProblem(result.Error!);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CourseUpdateDto dto)
    {
        var result = await _courseService.UpdateAsync(id, dto);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _courseService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
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
