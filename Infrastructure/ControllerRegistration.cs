using Microsoft.AspNetCore.Mvc;

namespace BirdieBuddy.Infrastructure;

public static class ControllerRegistration
{
    public static IMvcBuilder AddBirdieBuddyControllers(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "urn:birdiebuddy:problem:validation.invalid_request",
                Instance = context.HttpContext.Request.Path
            };
            problem.Extensions["code"] = "validation.invalid_request";
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        });
        // AddControllers alone omits the MVC services needed to instantiate this filter.
        return services.AddControllersWithViews(options =>
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
    }
}
