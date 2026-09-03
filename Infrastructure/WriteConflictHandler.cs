using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BirdieBuddy.Services;

namespace BirdieBuddy.Infrastructure;

public sealed class WriteConflictHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        var conflict = exception is DbUpdateConcurrencyException ||
            exception is DbUpdateException { InnerException: PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Holes_RoundId_HoleNumber" } };
        if (!conflict) return false;
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 409, Title = "Conflicting update", Detail = RoundService.ConflictMessage,
            Extensions = { ["code"] = "round_conflict" }
        }, cancellationToken: token);
        return true;
    }
}
