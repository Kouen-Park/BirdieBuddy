using System.Security.Claims;
using BirdieBuddy.Services;

namespace BirdieBuddy.Infrastructure;

public sealed class MobileBearerMiddleware
{
    private readonly RequestDelegate _next;

    public MobileBearerMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IMobileTokenService tokens)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var raw = header["Bearer ".Length..].Trim();
            var user = await tokens.FindAccessUserAsync(raw);
            if (user is not null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.DisplayName),
                    new Claim("birdiebuddy.session-version", user.SessionVersion.ToString()),
                    new Claim("birdiebuddy.auth-scheme", "mobile-bearer")
                };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "MobileBearer"));
            }
        }

        await _next(context);
    }
}
