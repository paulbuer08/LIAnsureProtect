using System.Diagnostics;
using System.Diagnostics.Metrics;
using LIAnsureProtect.Platform.Abstractions.Observability;
using Microsoft.AspNetCore.Routing;

namespace LIAnsureProtect.Api.Observability;

public sealed partial class RequestOutcomeMiddleware(
    RequestDelegate next,
    ILogger<RequestOutcomeMiddleware> logger)
{
    private static readonly Meter Meter = new(ObservabilityNames.MeterName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(ObservabilityNames.ApiRequestsMetric);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(ObservabilityNames.ApiRequestDurationMetric, "ms");

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var statusClass = $"{context.Response.StatusCode / 100}xx";
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var tags = new TagList
            {
                { "http.request.method", context.Request.Method },
                { "http.response.status_class", statusClass }
            };
            Requests.Add(1, tags);
            Duration.Record(elapsedMs, tags);

            RequestCompleted(
                logger,
                context.Request.Method,
                route,
                context.Response.StatusCode,
                elapsedMs,
                context.TraceIdentifier);
        }
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "HTTP request completed. Method: {Method}; Route: {Route}; StatusCode: {StatusCode}; DurationMs: {DurationMs}; CorrelationId: {CorrelationId}")]
    private static partial void RequestCompleted(
        ILogger logger,
        string method,
        string route,
        int statusCode,
        double durationMs,
        string correlationId);
}
