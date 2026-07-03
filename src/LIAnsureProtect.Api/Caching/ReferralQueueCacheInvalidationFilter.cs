using LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;
using LIAnsureProtect.Platform.Abstractions.Caching;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace LIAnsureProtect.Api.Caching;

/// <summary>
/// Evicts the shared referral-queue cache entry after a successful unsafe (write) request, so
/// underwriters always read their own actions immediately (read-your-writes) while the 10-second
/// TTL covers Worker-side (already eventually-consistent) projection changes.
///
/// Applied at the API edge — the one place every synchronous queue-affecting write passes through
/// (referral operations, decisions, evidence, quote creation) — so no module has to know a cache
/// exists. Applied per controller via <c>[ServiceFilter]</c>; reads and failed writes are ignored.
/// </summary>
public sealed class ReferralQueueCacheInvalidationFilter(ICacheService cacheService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (!HttpMethods.IsPost(context.HttpContext.Request.Method)
            && !HttpMethods.IsPut(context.HttpContext.Request.Method)
            && !HttpMethods.IsPatch(context.HttpContext.Request.Method)
            && !HttpMethods.IsDelete(context.HttpContext.Request.Method))
        {
            return;
        }

        if (executed.Exception is not null && !executed.ExceptionHandled)
            return;

        // Only a completed write should evict (4xx/5xx results left the queue unchanged).
        if (executed.Result is IStatusCodeActionResult { StatusCode: >= 400 })
            return;

        await cacheService.RemoveAsync(
            ListQuoteReferralsQuery.QueueCacheKey,
            context.HttpContext.RequestAborted);
    }
}
