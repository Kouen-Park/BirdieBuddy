using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;

namespace BirdieBuddy.Controllers;

[ApiController]
[Authorize]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly IGolfNzImportJob _golfNzImportJob;
    private readonly IConfiguration _configuration;

    public CoursesController(
        ICourseService courseService,
        IGolfNzImportJob golfNzImportJob,
        IConfiguration configuration)
    {
        _courseService = courseService;
        _golfNzImportJob = golfNzImportJob;
        _configuration = configuration;
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
        if (!IsImportAdministrator()) return NotFound();
        var started = _golfNzImportJob.TryStart(out var status);
        return started ? Accepted(status) : Ok(status);
    }

    [HttpGet("import-golf-nz/status")]
    public ActionResult<GolfNzImportJobStatus> GetGolfNzImportStatus()
    {
        if (!IsImportAdministrator()) return NotFound();
        return Ok(_golfNzImportJob.GetStatus());
    }

    private bool IsImportAdministrator()
    {
        var configured = _configuration["Administration:ImportKey"];
        var supplied = Request.Headers["X-Admin-Key"].ToString();
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(supplied)) return false;
        var configuredHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(configured));
        var suppliedHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(supplied));
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(configuredHash, suppliedHash);
    }

}
