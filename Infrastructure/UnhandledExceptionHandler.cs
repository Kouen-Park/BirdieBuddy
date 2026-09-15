using Microsoft.AspNetCore.Diagnostics;

namespace BirdieBuddy.Infrastructure;

public sealed class UnhandledExceptionHandler : IExceptionHandler
{
    private readonly ILogger<UnhandledExceptionHandler> _logger;

    public UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        _logger.LogError(exception, "Unhandled request exception for {Path}.", context.Request.Path);
        await ApiErrors.WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
            "internal.error", "Something went wrong.",
            "The request could not be completed. Try again, and contact support if the problem continues.", token);
        return true;
    }
}
