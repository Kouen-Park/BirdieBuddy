using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BirdieBuddy.Infrastructure;

public static class TelemetryRegistration
{
    public static void AddBirdieBuddyTelemetry(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) return;

        var resource = ResourceBuilder.CreateDefault().AddService(
            serviceName: "birdie-buddy",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder.AddService("birdie-buddy"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    // Restrict tracing to API paths so email verification/reset tokens
                    // in static-page query strings never enter exported spans.
                    options.Filter = context => context.Request.Path.StartsWithSegments("/api"))
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter());

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resource);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.AddOtlpExporter();
        });
    }
}
