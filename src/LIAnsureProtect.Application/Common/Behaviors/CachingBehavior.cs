using LIAnsureProtect.Platform.Abstractions.Caching;
using MediatR;

namespace LIAnsureProtect.Application.Common.Behaviors;

/// <summary>
/// Cache-aside pipeline behavior. Requests that implement <see cref="ICacheableRequest"/> have
/// their response cached under the request's key/TTL; every other request passes straight through
/// to its handler. Nothing is cached unless it explicitly opts in.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>(ICacheService cacheService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableRequest cacheable)
            return await next(cancellationToken);

        return await cacheService.GetOrCreateAsync<TResponse>(
            cacheable.CacheKey,
            token => next(token),
            cacheable.CacheTtl,
            cancellationToken);
    }
}
