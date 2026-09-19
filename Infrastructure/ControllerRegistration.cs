using Microsoft.AspNetCore.Mvc;

namespace BirdieBuddy.Infrastructure;

public static class ControllerRegistration
{
    public static IMvcBuilder AddBirdieBuddyControllers(this IServiceCollection services)
    {
        // Register MVC first. Its ApiBehaviorOptions setup installs the framework
        // default response factory, so our application contract must be configured
        // afterwards to remain the final value.
        var mvc = services.AddControllersWithViews(options =>
        {
            // Browser cookie sessions still require the antiforgery token; requests
            // authenticated by a mobile bearer token are exempt because that credential
            // is never sent ambiently. See MobileAwareAntiforgeryFilter.
            options.Filters.Add<MobileAwareAntiforgeryFilter>();
            options.Filters.Add(new AntiforgeryProblemDetailsFilter());
        });

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
        return mvc;
    }
}
