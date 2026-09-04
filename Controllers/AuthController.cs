using System.Security.Claims;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace BirdieBuddy.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAccountEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IAccountEmailSender emailSender,
        IConfiguration configuration, ILogger<AuthController> logger)
    {
        _authService = authService;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterDto dto)
    {
        var (user, error) = await _authService.RegisterAsync(dto);
        if (error is not null)
            return Problem(detail: error, statusCode: error.Contains("already exists") ? 409 : 400, title: "Account could not be created.");

        await SignInAsync(user!);
        await SendVerificationAsync(user!.Id);
        return Ok(user);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginDto dto)
    {
        var user = await _authService.ValidateLoginAsync(dto);
        if (user is null) return Problem(detail: "Email or password is incorrect.", statusCode: 401, title: "Sign in failed.");

        await SignInAsync(user);
        return Ok(user);
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId)) return Unauthorized();

        var user = await _authService.GetByIdAsync(userId);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPut("profile")]
    public async Task<ActionResult<CurrentUserDto>> UpdateProfile(UpdateProfileDto dto)
    {
        var (user, error) = await _authService.UpdateProfileAsync(CurrentUserId(), dto);
        return error is null ? Ok(user) : Problem(detail: error, statusCode: 400, title: "Profile could not be updated.");
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var error = await _authService.ChangePasswordAsync(CurrentUserId(), dto);
        return error is null ? NoContent() : Problem(detail: error, statusCode: 400, title: "Password could not be changed.");
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(RequestPasswordResetDto dto)
    {
        var (email, token) = await _authService.CreatePasswordResetAsync(dto.Email);
        if (email is not null && token is not null)
            await TrySendAsync(() => _emailSender.SendPasswordResetAsync(email, BuildUrl("reset-password.html", token)));
        return Accepted(new { message = "If that address belongs to an account, a reset email has been sent." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var error = await _authService.ResetPasswordAsync(dto);
        return error is null ? NoContent() : Problem(detail: error, statusCode: 400, title: "Password could not be reset.");
    }

    [HttpPost("send-verification")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SendVerification()
    {
        await SendVerificationAsync(CurrentUserId());
        return Accepted();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailDto dto)
    {
        var error = await _authService.VerifyEmailAsync(dto);
        return error is null ? NoContent() : Problem(detail: error, statusCode: 400, title: "Email could not be verified.");
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var export = await _authService.ExportAsync(CurrentUserId());
        if (export is null) return Unauthorized();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
        return File(bytes, "application/json", $"birdie-buddy-export-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    [HttpPost("delete-account")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountDto dto)
    {
        var error = await _authService.DeleteAccountAsync(CurrentUserId(), dto);
        if (error is not null)
            return Problem(detail: error, statusCode: 400, title: "Account could not be deleted.");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private async Task SendVerificationAsync(int userId)
    {
        var (email, token) = await _authService.CreateEmailVerificationAsync(userId);
        if (email is not null && token is not null)
            await TrySendAsync(() => _emailSender.SendEmailVerificationAsync(email, BuildUrl("verify-email.html", token)));
    }

    private string BuildUrl(string page, string token)
    {
        var configured = _configuration["Application:PublicBaseUrl"]?.TrimEnd('/');
        var origin = string.IsNullOrWhiteSpace(configured) ? $"{Request.Scheme}://{Request.Host}" : configured;
        return $"{origin}/{page}?token={Uri.EscapeDataString(token)}";
    }

    private async Task TrySendAsync(Func<Task> send)
    {
        try { await send(); }
        catch (Exception exception) { _logger.LogError(exception, "Account email delivery failed."); }
    }

    private async Task SignInAsync(CurrentUserDto user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
