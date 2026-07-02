using System.Text.RegularExpressions;
using LIAnsureProtect.Platform.Abstractions.Observability;

namespace LIAnsureProtect.Api.Observability;

public sealed partial class RequestCorrelationMiddleware(
    RequestDelegate next,
    ILogger<RequestCorrelationMiddleware> logger)
{
    private const int MaxCorrelationIdLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[ObservabilityNames.CorrelationIdHeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString(),
            ["RequestPath"] = SanitizeForLog(context.Request.Path.Value),
            ["RequestMethod"] = SanitizeForLog(context.Request.Method)
        });

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ObservabilityNames.CorrelationIdHeaderName, out var values))
        {
            // Client-supplied value: strip everything outside a strict allowlist so it can never
            // carry newlines or control characters into logs or response headers.
            var sanitized = UnsafeCorrelationIdCharacters().Replace(values.FirstOrDefault() ?? string.Empty, string.Empty);
            if (sanitized.Length > MaxCorrelationIdLength)
                sanitized = sanitized[..MaxCorrelationIdLength];

            if (sanitized.Length > 0)
                return sanitized;
        }

        return Guid.NewGuid().ToString("N");
    }

    // Values that reach log scopes from the request must not be able to forge log entries.
    private static string? SanitizeForLog(string? value) =>
        value is null ? null : LineBreakCharacters().Replace(value, string.Empty);

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeCorrelationIdCharacters();

    [GeneratedRegex("[\\r\\n]")]
    private static partial Regex LineBreakCharacters();
}
