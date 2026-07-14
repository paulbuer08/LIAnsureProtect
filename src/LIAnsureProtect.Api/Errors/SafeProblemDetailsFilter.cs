using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LIAnsureProtect.Api.Errors;

public sealed class SafeProblemDetailsFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ProblemDetails problem })
            return;

        problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;

        // Expected 4xx domain and validation details are part of the established API contract.
        // Unexpected 5xx details are diagnostic data and must never cross the public boundary.
        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            problem.Detail = null;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
