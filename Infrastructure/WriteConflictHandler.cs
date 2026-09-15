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
        await ApiErrors.WriteProblemAsync(context, StatusCodes.Status409Conflict,
            "round.save_conflict", "The record changed elsewhere.", RoundService.ConflictMessage, token);
        return true;
    }
}
