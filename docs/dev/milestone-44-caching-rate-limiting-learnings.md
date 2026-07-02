# Milestone 44 — Caching + Rate Limiting (Learnings)

## What we set out to do

Add three production-hardening capabilities — a cache-aside abstraction (`ICacheService`:
in-memory locally, Redis under `Platform:Profile=Aws`), request rate limiting (HTTP 429), and
security-headers middleware — **without changing any business flow, endpoint contract, EF model,
schema, or the frontend**, and **without an AWS account** (local Docker Redis).

## What shipped

- `Microsoft.Extensions.Caching.StackExchangeRedis` + `.Memory` (Infrastructure).
- `ICacheService` port (`GetOrCreateAsync`/`RemoveAsync`) in `Platform.Abstractions.Caching`;
  `InMemoryCacheService` (IMemoryCache) and `RedisCacheService` (IDistributedCache, JSON, key-prefixed);
  profile switch in `AddInfrastructure` with fail-fast on a missing Redis connection under Aws.
- `ICacheableRequest` marker + `CachingBehavior<TReq,TResp>` MediatR behavior (registered in
  `AddApplication`) — cache-aside opt-in per request; inert for everything else.
- `CacheOptions` bound from the `Cache` section in both hosts.
- Rate limiting in the API: a global fixed-window `PartitionedRateLimiter` keyed per authenticated
  user (client-IP fallback), stricter for unsafe methods, `429` + `ProblemDetails` + `Retry-After`,
  limits from `RateLimitingOptions` (generous defaults, config-tunable).
- `SecurityHeadersMiddleware` (nosniff, frame-deny, referrer, CSP, permissions-policy).
- Tests: in-memory adapter (hit/miss/invalidation/TTL), caching behavior (routing), cache
  profile-switch, security headers, 429 behavior; env-gated Redis round-trip; `redis` compose service.

## Decisions & why

### Nothing in production is cached yet — on purpose
Every current read is either **per-user/PII** (evidence, submissions, inbox) or a
**freshness-critical live queue** (the underwriter referral/SLA queue). Caching those would serve
stale data and make the existing endpoint tests flaky — the exact "choke" this program forbids. So
M44 ships and proves the *mechanism* (adapters + behavior + invalidation) and leaves adoption on a
specific read to a deliberate follow-up paired with write-triggered invalidation. Recorded as a
conscious trade in the design doc.

### Cache-aside as an opt-in pipeline behavior
`CachingBehavior` caches only requests implementing `ICacheableRequest` (which supplies key + TTL);
all other requests pass straight through. This makes adoption a one-line marker on a query with
zero risk to anything not marked — and keeps caching policy next to the query it applies to.

### Rate limits read from options per request, not captured at registration
The first attempt captured `RateLimiting:*` as ints at service-registration time. Under
`WebApplicationFactory`, test config overrides are applied at **build** time — after registration —
so the captured ints missed them and the 429 test saw the default limit. Fix: bind
`RateLimitingOptions` and read `IOptions<RateLimitingOptions>` inside the partition factory
(per request), so any configuration applied after registration (production env, test overrides) is
honored. This is also why production can tune limits without a redeploy of code.

### Generous default limits so normal traffic never trips
Defaults are high (safe 5000, unsafe 2000 per 60s per caller). The full existing suite (~60 endpoint
tests firing many requests) was re-run and stays green — empirical proof that only genuine floods
are limited. Production tightens via `RateLimiting:*` config/env.

### Fail fast on missing Redis connection
Same stance as S3 (M42) and SNS (M43): under the Aws profile, resolving the cache throws if
`Cache:RedisConnectionString` is missing, rather than silently wiring a connectionless client.

## Gotchas

- **Config timing under WebApplicationFactory** (above) — the key lesson: middleware that must
  respect test/production config should read options at request time, not capture values at startup.
- **Redis lazy connect**: constructing `RedisCache` with a connection string does not connect until
  the first operation, so the profile-switch test resolves `RedisCacheService` with no live server.
- **Package versions**: caching packages resolved to `10.0.9` (framework-aligned), distinct from the
  `AWSSDK` v4 line; all pinned via central package management.
- **CI stays green**: the Redis round-trip test is env-gated (`LIANSUREPROTECT_RUN_REDIS_TESTS`) and
  skipped by default; `redis` is profile-scoped in compose (`aws-local`).

## Verification

- `dotnet test`: UnitTests 68 passed; IntegrationTests 150 passed, 4 skipped (PostgreSQL, S3, SNS,
  Redis opt-ins). Security-headers and 429 tests pass; the full suite proves the limiter never trips
  normal traffic.
- Redis round-trip run locally with `LIANSUREPROTECT_RUN_REDIS_TESTS=true` against
  `docker compose --profile aws-local up -d redis` — stores, re-reads (factory once), evicts, rebuilds.

## What M44 deliberately left for later

- Adopting cache-aside on specific production reads (with write-triggered invalidation).
- WAF / bot management (edge, provisioned with CloudFront/ALB in Phase 2).
- ElastiCache provisioning (Terraform, Phase 2); distributed (Redis-backed) rate limiting for
  multi-instance scale-out.
