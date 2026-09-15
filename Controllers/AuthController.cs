using System.Security.Claims;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using BirdieBuddy.Infrastructure;

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
        var result = await _authService.RegisterAsync(dto);
        if (!result.IsSuccess) return this.ApiProblem(result.Error!);
        var user = result.Value!;

        await SendVerificationAsync(user.Id);
        if (_configuration.GetValue("Authentication:RequireVerifiedEmail", false))
            return Accepted(new { requiresEmailVerification = true, email = user.Email });
        await SignInAsync(user);
        return Ok(user);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginDto dto)
    {
        var user = await _authService.ValidateLoginAsync(dto);
        if (user is null) return this.ApiProblem(401, "auth.invalid_credentials", "Sign in failed.", "Email or password is incorrect.");
        if (_configuration.GetValue("Authentication:RequireVerifiedEmail", false) && !user.EmailVerified)
        {
            await SendVerificationAsync(user.Id);
            return this.ApiProblem(403, "auth.email_unverified", "Email verification required.",
                "Check your inbox for a verification link before signing in.");
        }

        await SignInAsync(user);
        return Ok(user);
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId)) return this.ApiProblem(401, "auth.required", "Authentication required.");

        var user = await _authService.GetByIdAsync(userId);
        return user is null ? this.ApiProblem(401, "auth.required", "Authentication required.") : Ok(user);
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
        var result = await _authService.UpdateProfileAsync(CurrentUserId(), dto);
        return result.IsSuccess ? Ok(result.Value) : this.ApiProblem(result.Error!);
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var result = await _authService.ChangePasswordAsync(CurrentUserId(), dto);
        if (!result.IsSuccess) return this.ApiProblem(result.Error!);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(RequestPasswordResetDto dto)
    {
        var (email, token) = await _authService.CreatePasswordResetAsync(dto.Email ?? string.Empty);
        if (email is not null && token is not null)
            await TrySendAsync(() => _emailSender.SendPasswordResetAsync(email, BuildUrl("reset-password.html", token)));
        return Accepted(new { message = "If that address belongs to an account, a reset email has been sent." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var result = await _authService.ResetPasswordAsync(dto);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
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
        var result = await _authService.VerifyEmailAsync(dto);
        return result.IsSuccess ? NoContent() : this.ApiProblem(result.Error!);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var export = await _authService.ExportAsync(CurrentUserId());
        if (export is null) return this.ApiProblem(401, "auth.required", "Authentication required.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
        return File(bytes, "application/json", $"birdie-buddy-export-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    [HttpPost("delete-account")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountDto dto)
    {
        var result = await _authService.DeleteAccountAsync(CurrentUserId(), dto);
        if (!result.IsSuccess) return this.ApiProblem(result.Error!);
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
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim("birdiebuddy.session-version", user.SessionVersion.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
