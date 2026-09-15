using System.Security.Claims;
using BirdieBuddy.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Infrastructure;

public static class SessionCookieValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var version = context.Principal?.FindFirstValue("birdiebuddy.session-version");
        if (!int.TryParse(userId, out var parsedUserId) || !int.TryParse(version, out var parsedVersion))
        {
            context.RejectPrincipal();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var currentVersion = await db.Users.AsNoTracking()
            .Where(user => user.Id == parsedUserId)
            .Select(user => (int?)user.SessionVersion)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (currentVersion is null || currentVersion.Value != parsedVersion)
            context.RejectPrincipal();
    }
}
