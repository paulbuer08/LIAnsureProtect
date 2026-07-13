using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Errors;

public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        UnhandledRequest(logger, httpContext.TraceIdentifier, exception);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "The request could not be completed.",
                Detail = "Please try again. If the problem continues, contact support with the support ID.",
                Extensions =
                {
                    ["code"] = "request.unexpected_error",
                    ["correlationId"] = httpContext.TraceIdentifier
                }
            }
        });
    }

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "Unhandled request failure. CorrelationId: {CorrelationId}")]
    private static partial void UnhandledRequest(
        ILogger logger,
        string correlationId,
        Exception exception);
}
