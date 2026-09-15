using Microsoft.AspNetCore.Mvc;
using BirdieBuddy.Services;

namespace BirdieBuddy.Infrastructure;

public static class ApiErrors
{
    public static ObjectResult ApiProblem(this ControllerBase controller, ServiceError error) =>
        controller.ApiProblem(error.Status, error.Code, error.Title, error.Detail);

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

    public static ProblemDetails CreateProblem(HttpContext context, int status, string code,
        string title, string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"urn:birdiebuddy:problem:{code}",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }

    public static async Task WriteProblemAsync(HttpContext context, int status, string code,
        string title, string? detail, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            CreateProblem(context, status, code, title, detail),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }
}
