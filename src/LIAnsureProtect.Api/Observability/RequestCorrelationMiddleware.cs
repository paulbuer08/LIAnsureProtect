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

    private const int MaxCorrelationIdLength = 64;

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ObservabilityNames.CorrelationIdHeaderName, out var values))
        {
            // Client-supplied value: rebuild it from an allowlist of characters so it can
            // never carry newlines or control characters into logs or response headers.
            var sanitized = new string((values.FirstOrDefault() ?? string.Empty)
                .Where(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
                .Take(MaxCorrelationIdLength)
                .ToArray());

            if (sanitized.Length > 0)
                return sanitized;
        }

        return Guid.NewGuid().ToString("N");
    }
}
