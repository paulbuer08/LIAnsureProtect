# Proposed Milestone — Referral Queue Hardening (Cache + Optimistic Concurrency)

Status: **fully-baked spec, not yet scheduled.** Recommended slot: a small standalone milestone
immediately **before or right after M45 (Terraform Foundation)** — it is one session of work and
has no AWS dependency. Number is assigned at kickoff to avoid renumbering the Phase-2 roadmap.

## Why (recap of the production reasoning)

The underwriter referral queue (`GET /api/v1/underwriting/quote-referrals`,
`ListQuoteReferralsQueryHandler`) is the hottest read in the app: every underwriter polls it, and
each call fans out to three readers (quotes + operations summaries + evidence summaries). It is a
legitimate production cache target **only if correctness moves to the write side first**:

> Never let a cache guarantee correctness. Make the write correct; then caching the read is safe.

Today `AssignQuoteReferralToMeCommand` has **no optimistic concurrency** — two underwriters
clicking "Assign to me" at the same moment can both believe they won. That is the real defect;
display staleness is only a symptom. This milestone fixes the write, then caches the read.

## Part 1 — Optimistic concurrency on referral operations (the correctness half)

1. Add a **concurrency token** to `QuoteReferralOperation` using PostgreSQL's system column:
   `builder.Property(o => o.Version).IsRowVersion()` via Npgsql's `xmin` mapping
   (`UseXminAsConcurrencyToken`) — no schema migration needed (xmin already exists on every row).
2. `AssignQuoteReferralToMeCommand` becomes **conditional**: the domain method throws if
   `AssignedUnderwriterUserId` is already set to someone else, and a concurrent write that raced
   past the check surfaces as `DbUpdateConcurrencyException` at `SaveChangesAsync`.
3. The handler maps `DbUpdateConcurrencyException` → the existing conflict path → **409** with a
   ProblemDetails body like "This referral was just assigned to another underwriter." The same
   guard applies to `ReleaseAssignment` and `Triage` (last-writer-wins is not acceptable for
   assignment; triage may legitimately retry).
4. **Frontend**: on 409, refetch the queue and show a non-blocking notice ("Already taken —
   list refreshed"). TanStack Query makes this a few lines (invalidate + toast).

Tests: two-context integration test (both load the same unassigned operation; first save wins,
second gets `DbUpdateConcurrencyException` → 409 through the endpoint); regression tests for
release/triage; UI test for the refetch path.

## Part 2 — Short-TTL shared cache on the queue read (the performance half)

1. Mark `ListQuoteReferralsQuery` with `ICacheableRequest`:
   key `underwriting:referral-queue:v1`, TTL **10 seconds**. One entry serves every underwriter
   (the query is user-invariant), so hit ratio approaches 100% under polling.
2. **Write-triggered invalidation** so users still see their *own* actions immediately
   (read-your-writes): every referral-operation write path calls
   `ICacheService.RemoveAsync("underwriting:referral-queue:v1")` after `SaveChangesAsync`.
   The write paths are already funneled: the module's operation command handlers
   (assign/release/triage/notes/tasks) + the referral-operation projector (event-driven
   create/close/activity) + the evidence request/review handlers. Implement once as a small
   `IReferralQueueCacheInvalidator` helper injected into those handlers — one line each.
3. The 10-second TTL remains as the safety net for any missed path; staleness of that magnitude
   is invisible against minute/hour-scale SLA timers **and harmless because Part 1 made the
   write authoritative**.
4. **Test strategy (the reason this is a milestone, not a patch):** existing endpoint tests do
   list-after-write and must stay green. The invalidation in step 2 preserves read-your-writes,
   so they pass unchanged; add explicit tests that (a) a second read within TTL does not re-run
   the handler (counting decorator over the readers), (b) an assign/triage/evidence write evicts
   the key, (c) two different-user reads share one cache entry.

## Explicit non-goals

- No read model / materialized view yet — that is the documented long-term replacement
  (outbox-fed `referral_queue` projection) and becomes attractive when the 3-reader fan-out is
  the bottleneck even at 10s TTL. Revisit after real load exists.
- No distributed lock; optimistic concurrency is sufficient and cheaper.
- No caching of any per-user or PII read (unchanged M44 rule).

## Acceptance criteria

- Double-assign race provably impossible (test); 409 + refetch UX in place.
- Queue read served from cache within TTL; all writes invalidate; full suite green with zero
  changes to existing test assertions.
- Encyclopedia Ch 8 (underwriting flow) + Ch 11 (resiliency) updated; CHANGELOG/status/roadmap.
