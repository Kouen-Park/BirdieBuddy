using Microsoft.AspNetCore.Mvc;

namespace BirdieBuddy.Infrastructure;

public static class ApiErrors
{
    public static ObjectResult ApiProblem(this ControllerBase controller, int status, string code,
        string title, string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"urn:birdiebuddy:problem:{code}",
            Instance = controller.HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;
        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
