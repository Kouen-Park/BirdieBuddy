using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BirdieBuddy.Infrastructure;

public sealed class AntiforgeryProblemDetailsFilter : IAsyncAlwaysRunResultFilter, IOrderedFilter
{
    // Run before MVC's ClientErrorResultFilter changes the marker result into a
    // generic 400 response, otherwise the antiforgery failure cannot be identified.
    public int Order => -3000;

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is IAntiforgeryValidationFailedResult)
        {
            var problem = ApiErrors.CreateProblem(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "security.csrf_invalid",
                "Request verification failed.",
                "Refresh the page and try again.");

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" }
            };
        }

        await next();
    }
}
