using LIAnsureProtect.Platform.Abstractions.Caching;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

/// <summary>
/// The underwriting referral queue read. It is the hottest read in the app (every underwriter
/// polls it, and each call fans out to three readers), user-invariant (no per-user filtering), and
/// non-PII — so it is cached shared for a short TTL. Correctness does not depend on the cache:
/// assignment is guarded by the domain + an optimistic-concurrency token (Referral Queue
/// Hardening), and synchronous writes through the API evict the entry so underwriters still see
/// their own actions immediately.
/// </summary>
public sealed record ListQuoteReferralsQuery : IRequest<ListQuoteReferralsResult>, ICacheableRequest
{
    /// <summary>Shared by the API-edge invalidation filter — keep the two in lockstep.</summary>
    public const string QueueCacheKey = "underwriting:referral-queue:v1";

    public string CacheKey => QueueCacheKey;

    public TimeSpan CacheTtl => TimeSpan.FromSeconds(10);
}
