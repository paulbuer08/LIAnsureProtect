# Async / Await and Eventing Conventions (Global Best Practice)

> **Scope:** project-wide standard for all backend code (Domain, Application, Infrastructure, Api,
> Worker, modules, and tests). New code MUST follow it; existing code is brought into line
> opportunistically when touched. This is one of the project's global best practices — treat it like
> the Clean Architecture dependency rule and the module-boundary rule.

## Why this exists

The platform is a **modular monolith with a synchronous CQRS request/response core plus an
event-driven transactional outbox for asynchronous cross-context side-effects.** Getting asynchrony
right keeps it flexible, consistent, resilient, and performant. The rule is: **be asynchronous where
it adds value, stay synchronous where consistency or simplicity matters, and never adopt asynchrony
that trades a real correctness guarantee for marginal throughput.**

## Current baseline (audited)

As of Milestone 36 the codebase already has strong async hygiene, so this document mostly *codifies
existing practice*:

- No blocking-on-async anywhere in `src` (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()` — none).
- No `async void`, no `Task.Run` (no fake-async, no sync-over-async).
- Database I/O uses `...Async` EF Core APIs (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`)
  with `CancellationToken` threaded end-to-end through handlers and repositories.
- The `.ToList()`/`.ToArray()` calls in Infrastructure operate on **already-materialized in-memory
  collections** (after `ToListAsync`, over `ChangeTracker` entries, string splits) — not hidden
  synchronous DB round-trips.

Keep it that way.

## Async / await rules

1. **Async all the way down on I/O.** Any method that does I/O (database, HTTP, file, queue) is
   `async` and returns `Task`/`Task<T>` (or `ValueTask<T>` on hot, often-synchronous paths). Do not
   wrap synchronous CPU-bound or trivial logic in tasks just to "look async".
2. **Never block on async.** No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, no `Task.Run` to
   call async code from sync code. If a path needs to be async, make it async to the entry point.
3. **Always accept and thread `CancellationToken`.** Every async Application/Infrastructure method
   takes a `CancellationToken` and passes it to every awaited call. Controllers and handlers flow the
   request's token through. This is mandatory, not optional.
4. **No `async void`** except true event handlers; use `async Task` so exceptions are observable.
5. **Prefer EF Core async APIs** for queries and saves (`ToListAsync`, `SingleOrDefaultAsync`,
   `AnyAsync`, `SaveChangesAsync`). Reserve synchronous LINQ operators (`ToList`, `Select`, `Where`)
   for already-materialized in-memory collections.
6. **Don't parallelize work on a single `DbContext`.** A `DbContext` is not thread-safe; await DB
   calls sequentially (or use separate contexts/scopes for genuine parallelism).
7. **Stream large reads** with `IAsyncEnumerable<T>` / `AsAsyncEnumerable()` only where the result set
   is large enough to matter; default to a single awaited materialization otherwise.
8. **`ConfigureAwait`:** not required in this ASP.NET Core / Worker codebase (no legacy
   synchronization context). Keep call sites clean; do not sprinkle `ConfigureAwait(false)`.
9. **Tests are async too:** `async Task` test methods, await the system under test, and pass a real
   `CancellationToken` where the API takes one.

## Eventing conventions (how asynchrony crosses boundaries)

1. **Events at the seams, synchronous in the core.** A user's primary action against its own
   bounded-context aggregate (create/quote/approve/accept/bind, the underwriter's own operation
   actions) is a **synchronous** command returning an immediate, consistent result. Reads/queries are
   synchronous. **Cross-context side-effects** (notifications, read-model/audit projections, the
   referral-operation projection) go through **domain events + the transactional outbox → module
   projector**.
2. **One reliable delivery mechanism.** Persist a domain event to the outbox in the *same*
   transaction as the state change (the `ModuleDbContext`/`SubmissionDbContext` `SaveChangesAsync`
   template). The Worker's `OutboxDispatcher` delivers it to each downstream projector. **No
   distributed transactions.**
3. **Consumers are idempotent and self-healing.** Project idempotently on the **source
   outbox-message id** (dedupe), and prefer *create-if-missing* semantics so re-delivery and ordering
   races never corrupt state. Eventual consistency must never produce a user-visible negative effect
   (e.g. M36's `EnsureCreatedForReferralAsync` removes the "operation not yet projected" window).
4. **Events carry identity; projectors read the state they need.** Keep event payloads small (ids +
   the fact). If a projector needs richer data, read it back through a port (e.g. the M35
   `IUnderwritingQuoteContextReader`) — the source is already committed, so the read is consistent.
5. **Not event sourcing.** The outbox delivers events reliably; it is **not** the system of record.
   PostgreSQL remains the source of truth. State is stored, not rebuilt by replay.
6. **Choose consistency deliberately.** Use eventual consistency where a brief settle is harmless and
   buys decoupling/resilience/throughput. Keep strong consistency (synchronous, same transaction)
   where the user is waiting on the result or where a stale read would be a correctness problem.

## Decision quick-reference

| Situation | Style |
|---|---|
| User command on its own context's aggregate | Synchronous command, strong consistency |
| Query / read endpoint | Synchronous, as fresh as the read model allows |
| Validation / authorization | Synchronous, in the request path |
| "Context B must react to something context A did" | Domain event → outbox → projector (eventual) |
| Notifications, audit/read-model projections | Event-driven (eventual), idempotent consumer |
| External provider call (rating, etc.) | Async with resilience (retry/circuit-breaker) |
| Anything doing I/O | `async` + `CancellationToken`, async all the way down |

## Related

- `docs/concepts/` — Clean Architecture, Modular Monolith, Ports & Adapters, schema-per-module,
  deploy switch.
- `docs/dev/milestone-33-notifications-module-learnings.md` — the outbox → projector seam.
- `docs/dev/milestone-36-underwriting-referral-operations-design.md` — events applied to referral ops.
