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
    private readonly IConfiguration _configuration;

    public MobileAuthController(IMobileTokenService tokens, IConfiguration configuration)
    {
        _tokens = tokens;
        _configuration = configuration;
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
}
