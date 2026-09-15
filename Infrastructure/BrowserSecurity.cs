namespace BirdieBuddy.Infrastructure;

public static class BrowserSecurity
{
    // Must run before static files: HTML needs this policy as much as API responses do.
    public static IApplicationBuilder UseBirdieBuddySecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; font-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
                if (context.Request.Path.StartsWithSegments("/api"))
                    context.Response.Headers["Cache-Control"] = "no-store";
                else if (context.Request.Path == "/" ||
                         new[] { ".html", ".css", ".js", ".svg", ".ttf", ".woff", ".woff2" }.Contains(
                             Path.GetExtension(context.Request.Path.Value ?? ""), StringComparer.OrdinalIgnoreCase) ||
                         context.Request.Path.Value?.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase) == true)
                    // Stable asset URLs must be revalidated after a deployment.
                    // Conditional requests can still return 304; this does not clear offline drafts.
                    context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate";
                return Task.CompletedTask;
            });
            await next();
        });
}
