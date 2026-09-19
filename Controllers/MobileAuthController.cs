using System.Security.Claims;
using BirdieBuddy.DTOs;
using BirdieBuddy.Infrastructure;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BirdieBuddy.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/mobile/auth")]
public sealed class MobileAuthController : ControllerBase
{
    private readonly IMobileTokenService _tokens;
    private readonly IAuthService _authService;
    private readonly IAccountEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileAuthController> _logger;

    public MobileAuthController(IMobileTokenService tokens, IAuthService authService,
        IAccountEmailSender emailSender, IConfiguration configuration, ILogger<MobileAuthController> logger)
    {
        _tokens = tokens;
        _authService = authService;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<MobileSessionDto>> Register(RegisterDto request)
    {
        var registration = await _authService.RegisterAsync(request);
        if (!registration.IsSuccess) return this.ApiProblem(registration.Error!);
        var user = registration.Value!;

        await SendVerificationAsync(user.Id);

        if (_configuration.GetValue("Authentication:RequireVerifiedEmail", false))
            return Accepted(new { requiresEmailVerification = true, email = user.Email });

        // The account is usable immediately, so issue the token pair here instead of
        // making the client follow up with a separate sign-in request.
        var session = await _tokens.CreateSessionAsync(new MobileSessionRequest(request.Email, request.Password));
        return session.IsSuccess
            ? Ok(session.Value)
            : this.ApiProblem(401, "auth.invalid_credentials", "Sign in failed.", session.Error?.Detail);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("session")]
    public async Task<ActionResult<MobileSessionDto>> CreateSession(MobileSessionRequest request)
    {
        var result = await _tokens.CreateSessionAsync(request);
        if (!result.IsSuccess) return this.ApiProblem(401, "auth.invalid_credentials", "Sign in failed.", result.Error?.Detail);
        if (_configuration.GetValue("Authentication:RequireVerifiedEmail", false) && !result.Value!.User.EmailVerified)
        {
            await _tokens.RevokeAsync(result.Value.User.Id);
            return this.ApiProblem(403, "auth.email_unverified", "Email verification required.", "Verify your email before signing in.");
        }
        return Ok(result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<ActionResult<MobileSessionDto>> Refresh(MobileRefreshRequest request)
    {
        var result = await _tokens.RefreshAsync(request);
        return result.IsSuccess ? Ok(result.Value) : this.ApiProblem(401, "auth.refresh_required", "Sign in required.", result.Error?.Detail);
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return this.ApiProblem(401, "auth.required", "Authentication required.");
        await _tokens.RevokeAsync(userId);
        return NoContent();
    }

    private async Task SendVerificationAsync(int userId)
    {
        var (email, token) = await _authService.CreateEmailVerificationAsync(userId);
        if (email is null || token is null) return;
        try
        {
            await _emailSender.SendEmailVerificationAsync(email, BuildUrl("verify-email.html", token));
        }
        catch (Exception exception)
        {
            // A mail outage must not fail an otherwise successful registration.
            _logger.LogError(exception, "Account email delivery failed.");
        }
    }

    private string BuildUrl(string page, string token)
    {
        var configured = _configuration["Application:PublicBaseUrl"]?.TrimEnd('/');
        var origin = string.IsNullOrWhiteSpace(configured) ? $"{Request.Scheme}://{Request.Host}" : configured;
        return $"{origin}/{page}?token={Uri.EscapeDataString(token)}";
    }
}
