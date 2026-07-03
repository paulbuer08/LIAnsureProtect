using LIAnsureProtect.Api.Caching;
using LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;
using LIAnsureProtect.Platform.Abstractions.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace LIAnsureProtect.IntegrationTests.Caching;

/// <summary>
/// The API-edge invalidation filter evicts the shared referral-queue cache entry after a
/// successful unsafe (write) request — and only then: reads and failed writes must leave the
/// cache untouched.
/// </summary>
public sealed class ReferralQueueCacheInvalidationFilterTests
{
    private static (ReferralQueueCacheInvalidationFilter Filter, Mock<ICacheService> Cache) CreateFilter()
    {
        var cache = new Mock<ICacheService>();
        return (new ReferralQueueCacheInvalidationFilter(cache.Object), cache);
    }

    private static ActionExecutingContext CreateExecutingContext(string httpMethod)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
    }

    private static ActionExecutionDelegate CreateNext(ActionExecutingContext executingContext, IActionResult result)
    {
        var executedContext = new ActionExecutedContext(executingContext, [], controller: new object())
        {
            Result = result
        };

        return () => Task.FromResult(executedContext);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Evicts_The_Queue_Entry_After_A_Successful_Write(string method)
    {
        var (filter, cache) = CreateFilter();
        var context = CreateExecutingContext(method);

        await filter.OnActionExecutionAsync(context, CreateNext(context, new OkObjectResult(new object())));

        cache.Verify(
            service => service.RemoveAsync(ListQuoteReferralsQuery.QueueCacheKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Leaves_The_Cache_Alone_For_Reads()
    {
        var (filter, cache) = CreateFilter();
        var context = CreateExecutingContext("GET");

        await filter.OnActionExecutionAsync(context, CreateNext(context, new OkObjectResult(new object())));

        cache.Verify(
            service => service.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Leaves_The_Cache_Alone_When_The_Write_Fails()
    {
        var (filter, cache) = CreateFilter();
        var context = CreateExecutingContext("POST");

        await filter.OnActionExecutionAsync(
            context,
            CreateNext(context, new ConflictObjectResult(new ProblemDetails())));

        cache.Verify(
            service => service.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
