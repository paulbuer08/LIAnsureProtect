using LIAnsureProtect.Platform.Abstractions.Observability;

namespace LIAnsureProtect.Api.Observability;

public sealed class RequestCorrelationMiddleware(
    RequestDelegate next,
    ILogger<RequestCorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[ObservabilityNames.CorrelationIdHeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString(),
            ["RequestPath"] = context.Request.Path.Value,
            ["RequestMethod"] = context.Request.Method
        });

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ObservabilityNames.CorrelationIdHeaderName, out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return Guid.NewGuid().ToString("N");
    }
}
