using System.Security.Claims;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BirdieBuddy.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

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
