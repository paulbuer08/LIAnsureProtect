# Milestone 44 — Caching + Rate Limiting (Design)

## One-sentence goal

Add three production-hardening capabilities to the API — a **cache-aside abstraction**
(`ICacheService`: in-memory locally, Redis under `Platform:Profile=Aws`), **request rate limiting**
(HTTP 429 when a caller floods the API), and **security-headers middleware** — **without changing
any business flow, endpoint contract, EF model, schema, or the frontend**, and **without an AWS
account** (Redis is a local Docker container).

## Why this milestone exists (simple English)

- **Caching:** some reads are expensive to rebuild and rarely change. Recomputing them on every
  request wastes database time. A **cache** keeps the answer in fast memory for a short time so
  repeated reads are cheap. **Cache-aside** means: look in the cache; on a miss, do the real work
  and store the result; on a hit, return the stored copy.
- **Rate limiting:** a single client (buggy loop, scraper, or attacker) can hammer the API and
  starve everyone else. A **rate limiter** caps how many requests each caller may make per time
  window; over the cap it replies **429 Too Many Requests** instead of doing the work.
- **Security headers:** small HTTP response headers (e.g. `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`) tell browsers to refuse risky behavior (MIME-sniffing, clickjacking).
  They are cheap, standard hardening.

> **Analogy:** caching is a **notepad** by the phone — instead of walking to the archive for the
> same fact every call, you jot it down and read it back for a few seconds. Rate limiting is the
> **"one scoop per customer"** rule so one person can't empty the tub. Security headers are the
> **safety label** on the tub telling everyone how to handle it.

## Design rules that keep this safe

1. **The cache is a new port + adapters — nothing existing is rewired.** `ICacheService` lives in
   the shared kernel; `InMemoryCacheService` (Local) and `RedisCacheService` (Aws) implement it,
   selected by the profile — the same Ports & Adapters switch as S3 (M42) and SNS (M43).
2. **Cache-aside is opt-in per query, never automatic.** A generic `CachingBehavior` (MediatR
   pipeline) caches only requests that implement `ICacheableRequest` (which supplies the key + TTL).
   Nothing is cached unless it explicitly opts in.
3. **We deliberately do NOT cache any current production read.** Every existing read is either
   **per-user / PII-bearing** (evidence, submissions, inbox) or a **freshness-critical live queue**
   (the underwriter referral/SLA queue). Caching those would risk stale reads — the exact "choke"
   we must avoid — and would destabilize the existing endpoint tests. So M44 ships the *mechanism*
   fully tested, and the first real read is adopted later, deliberately, with write-triggered
   invalidation. (See "Why nothing is cached yet".)
4. **Rate limiting must not break existing behavior.** Limits are config-driven with **generous
   production defaults**; the limiter partitions per authenticated user (falling back to client IP).
   Normal traffic — and the existing test suite — never trips it; only a flood does.

## What gets built

### 1. Cache capability
- **`ICacheService`** (`Platform.Abstractions.Caching`): `GetOrCreateAsync<T>(key, factory, ttl, ct)`
  and `RemoveAsync(key, ct)` (the invalidation hook).
- **`InMemoryCacheService`** — wraps `IMemoryCache` (Local).
- **`RedisCacheService`** — wraps `IDistributedCache` (StackExchange.Redis), JSON-serialized values,
  key-prefixed (Aws).
- **`ICacheableRequest`** marker (`CacheKey` + `CacheTtl`) and **`CachingBehavior<TReq,TResp>`**
  MediatR behavior that caches only marked requests. Registered in `AddApplication` alongside the
  existing `ValidationBehavior`.
- **`CacheOptions`** (Redis connection string, key prefix) bound from the `Cache` config section.
- Profile switch in `AddInfrastructure`: Local → in-memory; Aws → Redis (fail-fast on a missing
  connection string).

### 2. Rate limiting (`Program.cs`)
- ASP.NET Core `AddRateLimiter` with a global partitioned **fixed-window** limiter:
  partition key = authenticated user id, else client IP.
- **Stricter window for unsafe methods** (POST/PUT/PATCH/DELETE) than for safe reads.
- Rejections return **429** with a `ProblemDetails` body and a `Retry-After` header.
- All limits/windows come from config (`RateLimiting:*`) with generous defaults so only genuine
  floods are limited.

### 3. Security headers middleware
- `SecurityHeadersMiddleware` sets `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: no-referrer`, a conservative `Content-Security-Policy`, and a restrictive
  `Permissions-Policy`, early in the pipeline for every response.

## Why nothing is cached yet (important)

Applying cache-aside to a live, test-covered, freshness-critical query (the underwriter referral
queue) would either serve stale SLA/assignment data or make the existing endpoint tests flaky —
both are "choking" the system, which this program explicitly forbids. The correct pattern is to
adopt caching **per read, deliberately, with matching invalidation on the write paths that change
it**. M44 delivers and proves the mechanism (adapters + behavior + invalidation) so that adoption
is a small, safe, well-tested follow-up rather than a risky big-bang. This is a conscious
engineering trade recorded here and in the learnings.

## How this is tested (and why CI stays green)

- **Cache unit tests** (normal CI): `InMemoryCacheService` hit/miss (factory runs once), `RemoveAsync`
  invalidation (factory re-runs after eviction), TTL expiry; `CachingBehavior` caches a marked fake
  request and passes non-marked requests straight through.
- **Cache profile-switch test**: Local wires in-memory, Aws wires Redis, missing connection fails fast.
- **Opt-in Redis round-trip test** (env-gated like the S3/SNS opt-ins): real `SET`/`GET`/`DEL`
  against a local Redis container; skipped by default. A `redis` compose service is added under the
  `aws-local` profile.
- **Security-headers test**: a normal response carries the expected headers.
- **Rate-limit test**: with a tiny configured limit, the Nth+1 request returns **429**; the full
  existing suite is re-run to prove generous defaults never trip normal traffic.

## Out of scope (later milestones)

- Adopting cache-aside on specific production reads (with write-triggered invalidation).
- WAF / bot management (edge concern, provisioned with CloudFront/ALB in Phase 2).
- ElastiCache provisioning (Terraform, Phase 2). M44 uses local Docker Redis only.
- Distributed rate limiting backed by Redis (the in-process limiter is per-instance; a shared
  store arrives when horizontal scale-out does).

## Acceptance criteria

- Cache invalidation and 429 tests pass in normal CI; the opt-in Redis round-trip passes where Redis runs.
- No existing test changes behavior; the full suite stays green.
- Encyclopedia Chapters 2, 3, and 11 updated; learnings, CHANGELOG, project-status, roadmap updated.
