namespace LIAnsureProtect.Api.Security;

/// <summary>
/// Adds conservative security response headers to every response. The API returns JSON only, so a
/// locked-down CSP and frame denial are safe and cheap defense-in-depth (MIME-sniffing,
/// clickjacking, referrer leakage).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";

        return next(context);
    }
}
