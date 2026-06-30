# Milestone 36 - Underwriting Referral Operations — Learning Notes

## Goal

Continue the Underwriting carve (after M35's AI review) by lifting **referral operations** — the
SLA/queue work-item an underwriter manages while a quote is referred (the `QuoteReferralOperation`
aggregate + work notes + follow-up tasks + timeline) — out of the legacy `Quotes` context into the
**Underwriting module** and the `underwriting` PostgreSQL schema. This was the most entangled slice so
far: the operation is created by Quoting, closed by the underwriting decision, projected into by
evidence activity, and read by a combined queue/timeline. See the design spec
(`milestone-36-underwriting-referral-operations-design.md`) and the plan
(`docs/superpowers/plans/2026-06-30-milestone-36-underwriting-referral-operations.md`).

## The decision that shaped everything: events, not synchronous ports

The hand-offs (create / close / evidence-projection) were implemented as **domain events** through the
existing outbox dispatcher → module projector seam (the M33 pattern), not synchronous ports. The
operation is now created/closed/updated **asynchronously**:

```
quote referred / decision recorded / evidence acted
   │  (writes its row + an outbox message in one legacy transaction)
   ▼
OutboxMessages (SubmissionDbContext)
   │  Worker poll loop
   ▼
OutboxDispatcher ──► INotificationProjector       (M33, unchanged)
                 └─► IReferralOperationProjector   (M36, NEW)
                       writes UnderwritingDbContext, idempotent on source outbox-message id
```

The projector reacts to events that **already existed** (no new event types): `QuoteGenerated`
(`Status == Referred`) → create, `QuoteUnderwritingDecisionRecorded` → close, and the six
`QuoteEvidenceRequest*` events → timeline/status. The underwriter's own actions
(assign/release/triage/note/task/complete) stayed **synchronous** MediatR commands that moved into the
module — they are direct user writes, not cross-context hand-offs.

This is **not** new architecture: the platform has been a hybrid (synchronous CQRS core + event-driven
outbox) since ~M14. M36 extended the existing seam to one more consumer. See
`docs/dev/async-and-eventing-conventions.md` (established this milestone as a global best practice).

## No fourth DbContext

M35 already created `UnderwritingDbContext` + the `underwriting` schema. M36 **added tables to it**, so
the dev scripts, the `common.ps1` migrations guard, and `ci.yml` were **unchanged** (still three
contexts). That removed a whole category of plumbing versus M33/M35.

## Three things we learned the hard way (worth remembering)

1. **The create event didn't carry what the aggregate needed.** `QuoteGeneratedDomainEvent` has only
   ids + status; `CreateDefault` needs the **risk tier** and **quote expiry**. Rather than fatten the
   event, the projector **reads them back** through the M35 `IUnderwritingQuoteContextReader` port
   (extended with `GetForReferralOperationAsync`). Pattern: *events carry identity; the consumer reads
   the state it needs* (the source is already committed, so the read is consistent). Keeps event
   payloads thin and reuses M35.

2. **Evidence creation secretly depended on the operation existing.** `QuoteEvidenceRequest.Create`
   requires a non-empty `quoteReferralOperationId`, which the evidence handler used to get by loading
   the legacy operation. Once the legacy operation stopped being written (it's now async + module-
   owned), that load returned null and would have thrown for **every** referred quote — and no
   dispatcher pump fixes it, because the pump creates the *module* operation, not the legacy one. The
   column turned out to be **vestigial** (nothing queries it; it only backed the now-dropped FK), so we
   **decoupled evidence-create** from the operation and correlate the column by the **quote id**. The
   cross-schema FK was dropped (reference by id only); the column is retained and is removed when
   evidence carves into the module in **M37**. This was the entanglement the design flagged as hard,
   and it surfaced mid-execution — a good example of why the carve is atomic.

3. **Idempotency + ordering.** The projector dedupes on the **source outbox-message id** (a small
   `referral_operation_projected_messages` table) and is **create-if-missing** so a referred quote's
   operation appears with no user-visible gap and late events self-heal. A closed-operation guard
   turned out to be unnecessary: the dispatcher processes outbox messages in `CreatedAtUtc` order, so
   evidence events (created before a decision) always project before the close — evidence-after-close
   can't occur through ordered delivery.

## Reads stayed legacy behind one port

`ListQuoteReferrals` (the queue) and the timeline read are fundamentally the Quoting referral queue and
genuinely span contexts, so they **stayed in legacy Application** and read the operation side through
the module's `IReferralOperationsReader` (`GetSummariesAsync` / `GetTimelineAsync`). The timeline still
concats the legacy underwriting-decision audit. This introduced one new allowed reference edge —
legacy `Application → Modules.Underwriting.Application` — captured in the
`ProjectReferenceBoundaryTests` ratchet (the same way M35 updated the Infrastructure/Api/Worker rows).

## Eventual consistency = a real (mitigated) behaviour change

Create/close/evidence projection are now eventually consistent. The mitigation (create-if-missing) means
no user-visible gap on the write path; reads settle a beat after the dispatcher runs. The integration
tests were reworked from synchronous assertions to **pump-then-assert** (a `PumpOutboxAsync` helper
drains `IOutboxDispatcher` before asserting). **No assertions were weakened** — only the timing of when
state is observed changed. Tests that drive the pumped dispatcher also needed SQLite fixtures for the
Notifications + Underwriting contexts (the dispatcher now runs their projectors in-test).

## Execution shape (strangler, always-buildable)

1. **Phase A** — build the module home fully (domain, ports, commands, EF mapping, repository/reader/
   projector, `CreateReferralOperations` migration) **beside** the untouched legacy aggregate. Suite
   stays green throughout.
2. **Phase B** — wire the events (outbox→referral mapper + dispatcher fan-out), remove the legacy
   synchronous writes, switch the controller + reads to the module, then rework the tests to pump. The
   build stayed 0/0 throughout; the integration suite had an **intentional red window** during the
   cut-over, closed by the test-rework task.
3. **Phase C** — delete the legacy aggregate/repo methods/configs, `DropReferralOperations` migration
   (drop-and-recreate; no production data), docs.

## Verification

- `dotnet build LIAnsureProtect.slnx` — 0 warnings / 0 errors.
- `dotnet test` — UnitTests 62, IntegrationTests 118 (+1 PostgreSQL opt-in skipped).
- `dotnet ef migrations has-pending-model-changes` — clean for **all three** contexts (Submission,
  Notifications, Underwriting).
- Full local CI (`scripts/run-local-ci.ps1`) before merge; delivered via the protected-`main` PR flow.

## What's next (remaining Underwriting carve)

- **Evidence** (requests / documents / reviews) — the largest sub-context; M37. It will also remove the
  vestigial `quote_referral_operation_id` column and finish the evidence↔operation decoupling.
- **The decision** (`ApproveReferral/DeclineReferral/AdjustReferral` + `QuoteUnderwritingReview`) — the
  hardest; stays on the `Quote` aggregate or moves via a Quoting command port, and is where
  `IUnitOfWork` likely relocates into `Platform.Abstractions`. (M36 did **not** need that relocation —
  the module commits writes through its own DbContext, mirroring M35.)
- **Integration-event / dispatcher decoupling** (fan-out registry, external bus) — M40.
