# Milestone 44.5 — Referral Queue Hardening (Design + Learnings)

Implements `docs/dev/referral-queue-hardening-spec.md` (numbered 44.5 so the Phase-2 roadmap
numbering M45+ stays untouched). One principle drove both halves:

> Never let a cache guarantee correctness. Make the write correct; then caching the read is safe.

## Part 1 — Optimistic concurrency on referral assignment

**The defect:** `QuoteReferralOperation.AssignTo` accepted any caller — two underwriters clicking
"Assign to me" simultaneously could both believe they won (last write silently overwrote).

**The fix, in three layers:**

1. **Domain guard** — `AssignTo` now rejects a second underwriter with
   `InvalidOperationException` ("already assigned"); the *same* underwriter re-clicking is an
   idempotent no-op; `ReleaseAssignment` remains the explicit hand-over path.
2. **Concurrency token** — a `Version` (bigint) property bumped by every mutation via a `Touch`
   helper, mapped with `IsConcurrencyToken()`. EF Core includes the original value in the UPDATE's
   WHERE clause, so a racing writer whose snapshot is stale (the domain guard can't see the other
   write) fails at `SaveChangesAsync` with `DbUpdateConcurrencyException`. A plain incremented
   column (not Npgsql's `xmin`) was chosen deliberately: it behaves identically on PostgreSQL
   (runtime) and SQLite (test harness). Migration: `AddReferralOperationConcurrencyVersion`.
3. **Conflict translation** — `EfReferralOperationRepository.SaveChangesAsync` translates
   `DbUpdateConcurrencyException` into the domain's conflict language
   (`InvalidOperationException`), which the API already maps to **409 Conflict** — no controller
   changes needed.

**Frontend:** the assign/release/triage mutations now also refetch the queue **on error**, so the
losing underwriter immediately sees the real assignee instead of a stale "unassigned" row; the
existing error panel shows the conflict message.

**Proof:** a domain unit test (guard + idempotency + version bumps), a two-context SQLite race test
(both load unassigned → first save wins → second save throws on the token → first assignment
survives), and an endpoint test (underwriter-2's assign → 409, underwriter-1 keeps the referral).

## Part 2 — 10-second shared cache on the queue read

`ListQuoteReferralsQuery` (the hottest read: every underwriter polls it; each call fans out to
three readers) now opts into cache-aside: key `underwriting:referral-queue:v1`, TTL 10s, one
shared entry for all underwriters (the query is user-invariant and non-PII).

**Read-your-writes** is preserved by an API-edge invalidation filter
(`ReferralQueueCacheInvalidationFilter`, applied via `[ServiceFilter]`) on the three controllers
whose successful unsafe requests can change the queue: the underwriting workbench controller
(operations/decisions/evidence), the owner evidence controller (responses/replacements), and the
quote-creation controller (new referrals). Reads and failed writes (4xx/5xx) never evict.

**Why the filter lives at the API edge:** it is the one place every *synchronous*
queue-affecting write passes through, so no module has to know a cache exists (boundaries stay
clean, no cross-module invalidator). Worker-side projector changes ride the 10s TTL — they were
already eventually consistent, so the TTL adds no new staleness class.

**Proof:** filter unit tests (evicts on successful POST/PUT/PATCH/DELETE; never on GET; never on
4xx), and — the strongest evidence — the **entire pre-existing endpoint suite passed unchanged**
with the cache active, because every list-after-write test exercises the invalidation path for
real.

## Gotchas

- The `Touch` refactor also runs in the constructor path, so new operations start at `Version 1`;
  tests assert *relative* increments, not absolute values.
- `[ServiceFilter]` requires the filter registered in DI (`AddScoped` in `Program.cs`).
- The filter checks `IStatusCodeActionResult.StatusCode >= 400` — a null status code (plain
  `Ok(...)`) counts as success.

## Deliberately not done (unchanged from the spec)

- No read model / materialized view for the queue yet — becomes attractive only when the 3-reader
  fan-out is a measured bottleneck even at 10s TTL.
- No distributed lock (optimistic concurrency is sufficient and cheaper) and no Redis-backed
  rate limiting (Phase-2 scale-out concern).

## Verification

`dotnet test`: UnitTests 74 passed, IntegrationTests 163 passed + 4 opt-in skips — including all
pre-existing referral/evidence endpoint tests unchanged. Frontend: ESLint clean, 35 tests passed,
build clean. Strict analyzer gate (warnings-as-errors) still zero-warning.
