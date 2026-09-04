using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace BirdieBuddy.Infrastructure;

public record RouteMetric(string Method, string Route, long Requests, long Failures,
    double FailureRatePercent, double AverageDurationMs, double MaximumDurationMs);
public record OperationalMetricsSnapshot(DateTime StartedAt, DateTime CapturedAt, long Requests,
    long Failures, double FailureRatePercent, List<RouteMetric> Routes);

public interface IOperationalMetrics
{
    void Record(string method, string route, int statusCode, double durationMs);
    OperationalMetricsSnapshot Snapshot();
}

public sealed class OperationalMetrics : IOperationalMetrics
{
    private sealed class Bucket { public long Requests; public long Failures; public long DurationMicros; public long MaxMicros; }
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private readonly DateTime _startedAt = DateTime.UtcNow;

    public void Record(string method, string route, int statusCode, double durationMs)
    {
        var bucket = _buckets.GetOrAdd($"{method} {route}", _ => new());
        Interlocked.Increment(ref bucket.Requests);
        if (statusCode >= 500) Interlocked.Increment(ref bucket.Failures);
        var micros = Math.Max(0, (long)(durationMs * 1000));
        Interlocked.Add(ref bucket.DurationMicros, micros);
        long current;
        while ((current = Volatile.Read(ref bucket.MaxMicros)) < micros &&
               Interlocked.CompareExchange(ref bucket.MaxMicros, micros, current) != current) { }
    }

    public OperationalMetricsSnapshot Snapshot()
    {
        var routes = _buckets.Select(pair =>
        {
            var split = pair.Key.IndexOf(' '); var bucket = pair.Value;
            var requests = Volatile.Read(ref bucket.Requests); var failures = Volatile.Read(ref bucket.Failures);
            return new RouteMetric(pair.Key[..split], pair.Key[(split + 1)..], requests, failures,
                Percent(failures, requests), requests == 0 ? 0 : Volatile.Read(ref bucket.DurationMicros) / 1000d / requests,
                Volatile.Read(ref bucket.MaxMicros) / 1000d);
        }).OrderByDescending(x => x.Requests).ThenBy(x => x.Route).ToList();
        var total = routes.Sum(x => x.Requests); var failures = routes.Sum(x => x.Failures);
        return new(_startedAt, DateTime.UtcNow, total, failures, Percent(failures, total), routes);
    }

    private static double Percent(long value, long total) => total == 0 ? 0 : Math.Round(value * 100d / total, 2);
}

public sealed class ApiObservabilityMiddleware
{
    private static readonly EventId RequestCompleted = new(1001, nameof(RequestCompleted));
    private static readonly EventId RequestFailed = new(1002, nameof(RequestFailed));
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiObservabilityMiddleware> _logger;

    public ApiObservabilityMiddleware(RequestDelegate next, ILogger<ApiObservabilityMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext context, IOperationalMetrics metrics)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await _next(context); return; }
        var stopwatch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });
        try { await _next(context); }
        catch (Exception exception)
        {
            _logger.LogError(RequestFailed, exception, "API request failed {Method} {Route} TraceId={TraceId}",
                context.Request.Method, Route(context), context.TraceIdentifier);
            throw;
        }
        finally
        {
            stopwatch.Stop(); var route = Route(context); var status = context.Response.StatusCode;
            metrics.Record(context.Request.Method, route, status, stopwatch.Elapsed.TotalMilliseconds);
            _logger.LogInformation(RequestCompleted,
                "API request completed {Method} {Route} StatusCode={StatusCode} DurationMs={DurationMs:F1} TraceId={TraceId}",
                context.Request.Method, route, status, stopwatch.Elapsed.TotalMilliseconds, context.TraceIdentifier);
        }
    }

    private static string Route(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
}

public static class ApiObservabilityExtensions
{
    public static IApplicationBuilder UseApiObservability(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiObservabilityMiddleware>();
}
