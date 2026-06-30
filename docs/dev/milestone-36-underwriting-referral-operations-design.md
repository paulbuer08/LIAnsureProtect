# Milestone 36 - Underwriting Referral Operations — Design Spec

> Status: **approved-for-planning** (design-first; written before any code).
> Branch: `feat/milestone-36-underwriting-referral-operations`.
> Continues the Underwriting carve started in M35 (AI review). This is the most entangled
> slice so far. Read alongside `docs/dev/milestone-35-underwriting-ai-review-module-learnings.md`.

## 1. Goal

Lift **referral operations** — the SLA/queue work-item that an underwriter manages while a quote is
referred — out of the legacy `Quotes` context and into the **Underwriting module** + the existing
`underwriting` PostgreSQL schema. The aggregate and its three child tables move; the underwriter's
own actions become module commands; and the three legacy callers that *create*, *close*, and
*project evidence into* the operation reach it through **domain events** (the M33 outbox-dispatcher →
module-projector pattern), not direct calls.

Behavior target: every existing endpoint keeps working and returns equivalent data. The one
deliberate semantic change is that create/close/evidence projection become **eventually consistent**
(operation state settles a beat after the Worker dispatches the triggering event), mitigated so there
is no user-visible negative effect (see §6).

## 2. What moves, and why the carve is atomic

`QuoteReferralOperation` is a single aggregate (root + `QuoteReferralWorkNote`,
`QuoteReferralFollowUpTask`, `QuoteReferralTimelineEntry`) in legacy `Domain/Quotes`, persisted by
`EfCoreQuoteRepository` via `IQuoteRepository` in the **public** schema. Six callers touch it:

```
                 ┌─────────────────────────────────────────────┐
                 │   QuoteReferralOperation (one aggregate)      │
                 │   + WorkNotes + Tasks + TimelineEntries       │
                 └─────────────────────────────────────────────┘
   writes ▲            ▲                ▲                  ▲ reads
          │            │                │                  │
 (1) CREATE      (2) CLOSE        (3) EVIDENCE       (4) QUEUE + TIMELINE
 CreateQuote     Approve/Decline/  7 call-sites in    ListQuoteReferrals
 Handler         Adjust handlers   evidence handlers  + GetQuoteReferralTimeline
```

It is **one table**. The instant it moves to the `underwriting` schema + the module assembly, all six
callers lose their `IQuoteRepository` access simultaneously — so the move is atomic and ships as
**one cohesive, behavior-preserving PR**. The only design levers are *how* each caller talks to the
moved aggregate and whether the two reads move or stay.

Good news from M35: `UnderwritingDbContext` + the `underwriting` schema already exist. M36 **adds
tables to that context** — there is **no fourth DbContext**, so `scripts/`, the `common.ps1`
migrations guard, and `ci.yml` are **unchanged** (still three contexts).

The aggregate currently takes Quoting types as method parameters (`CyberRiskTier`,
`QuoteUnderwritingDecision`, `EvidenceReviewDecisionStatus`). The module cannot reference those, so at
every seam they are passed as **strings** (the same convention M35 used for `Status == "Referred"`).
The cross-schema FK from `quote_evidence_requests` to `referral_operations` is **dropped** (reference
by id only — the modular-monolith rule, identical to M35 dropping the `Quote` FK).

## 3. The event seam (M33 pattern, extended)

The `OutboxDispatcher` runs in the **Worker**, reads `SubmissionDbContext.OutboxMessages`, and today
fans out to one downstream — `INotificationProjector` — idempotently on the source outbox-message id,
in the projector's own context/transaction, with **no distributed transaction**. M36 adds a **second
downstream**, an Underwriting `IReferralOperationProjector`, into the same loop:

```
 quote referred / decision recorded / evidence acted
        │  (writes its own row + an outbox message — one legacy transaction)
        ▼
 OutboxMessages (SubmissionDbContext)
        │   Worker poll loop
        ▼
 OutboxDispatcher ──► INotificationProjector       (M33, unchanged)
                  └─► IReferralOperationProjector   (M36, NEW)
                        writes UnderwritingDbContext, idempotent on source outbox-message id
```

The projector reacts to events that **already exist** (no new event types required):

| Domain event (legacy `Quotes`) | Projector action on the operation |
|---|---|
| `QuoteGeneratedDomainEvent` where `Status == Referred` | `EnsureCreatedForReferralAsync` (create-if-missing) |
| `QuoteUnderwritingDecisionRecordedDomainEvent` | `CloseForDecision(reviewedBy, decision-string, at)` |
| `QuoteEvidenceRequestCreatedDomainEvent` | `RecordEvidenceRequestCreated(...)` |
| `QuoteEvidenceRequestRespondedDomainEvent` | `RecordEvidenceRequestResponded(...)` |
| `QuoteEvidenceRequestAcceptedDomainEvent` | `RecordEvidenceRequestAccepted(...)` |
| `QuoteEvidenceRequestCancelledDomainEvent` | `RecordEvidenceRequestCancelled(...)` |
| `QuoteEvidenceRequestFollowUpSentDomainEvent` | `RecordEvidenceRequestFollowUpSent(...)` |
| `QuoteEvidenceRequestRemediationRequiredDomainEvent` | `RecordEvidenceRequestReviewDecision(...)` |

Idempotency: the projector records the **source outbox-message id** it has applied (a small
`underwriting.referral_operation_projected_messages` dedupe table, or an equivalent marker), so
re-delivery after a crash is a no-op — the same safety property M33 relies on.

> Mapping caveat (pinned during planning): the table above is the first cut. Today some legacy
> evidence paths record more than one timeline entry per action (e.g. accept records both an
> `EvidenceRequestAccepted` and an `EvidenceRequestReviewDecision` entry). The exact
> event → method mapping is finalized in the implementation plan so the resulting timeline entries
> match current behavior; if an existing path needs two entries, the projector emits both.

### The create-event data gap

`QuoteGeneratedDomainEvent` carries only `QuoteId, SubmissionId, OwnerUserId, Status` — it lacks the
**risk tier** and **quote expiry** that `CreateDefault` needs. Resolution: **the projector reads them
back** through the M35 `IUnderwritingQuoteContextReader` port (extended with `ExpiresAtUtc` and the
referred-at timestamp). This reuses M35 infrastructure and keeps events thin (event carries identity;
projector fetches the state it needs). The quote is committed before dispatch, so the read-back is
always consistent.

## 4. The underwriter's own actions stay synchronous commands

Assign, release, triage, add-note, add-task, complete-task are **direct user writes**, not
cross-context hand-offs. They move into the module as MediatR commands and are dispatched straight
from the controller (exactly as M35 proved cross-assembly MediatR works for AI review). They require
the operation to already exist; if it does not yet (the projector hasn't run), they call the same
`EnsureCreatedForReferralAsync` to self-heal — no duplicated creation logic, no 404 window (§6).

## 5. Reads stay legacy, behind one inbound read port

`ListQuoteReferrals` is fundamentally the Quoting **referral queue** (driven by pending-referral
quotes) and `GetQuoteReferralTimeline` legitimately spans two contexts (operation timeline + legacy
decision audit + legacy evidence summary). Both **stay in legacy Application** and read the
operation side through one inbound port the module exposes:

```
IReferralOperationsReader.GetSummariesAsync(quoteIds)  → queue operation summaries
IReferralOperationsReader.GetTimelineAsync(quoteId)    → operation timeline entries
```

Legacy keeps concatenating the decision-audit entries (`ListUnderwritingReviewsAsync`) and the
evidence summary it already owns. `CloseForDecision` keeps recording its existing `StatusChanged`
timeline entry only (no new `DecisionRecorded` entry on the operation), so the legacy
audit-synthesised `DecisionRecorded` entries do **not** double-count. Equivalent timeline, preserved.

## 6. Consistency model (the deliberate, mitigated change)

Eventual consistency is the model — this is how production event-driven systems behave; the dispatch
window is sub-second and a human underwriter does not perceive it. Mitigation so there is **no
negative effect**:

- **Single creation method**, `EnsureCreatedForReferralAsync(quoteId)`, is **create-if-missing**
  (idempotent). The event projector is the normal trigger; an underwriter write-command that arrives
  first calls the same method to self-heal. No 404 window on the write path.
- **Reads** (the queue) are purely eventually consistent: a just-referred quote shows with its
  operation summary filling in a beat later. This is normal and acceptable — the quote still appears.
- **Close / evidence** projection settles after dispatch; the queue/timeline reflect the prior state
  until the Worker runs. Acceptable for an SLA queue.

## 7. Async / eventing conventions (new project best-practice deliverable)

Per the M36 direction, add `docs/dev/async-and-eventing-conventions.md` capturing the standing
guidance and reference it from `docs/project-status.md`:

- Prefer `async`/`await` end-to-end on I/O paths; never block on async (`.Result`/`.Wait()`); always
  thread `CancellationToken`; avoid `async void` outside event handlers; return `Task`/`ValueTask`
  from I/O-bound APIs. Apply this **where it adds value** — do not wrap CPU-bound or trivially
  synchronous logic in tasks.
- Cross-context side effects go through **domain events + the outbox dispatcher → module projector**
  (idempotent on source message id, projector-owned transaction, no distributed transaction). A
  direct user write to a context's own aggregate stays a synchronous command.
- Design consumers to be **idempotent and self-healing** (create-if-missing, dedupe on source id) so
  eventual consistency never produces a user-visible negative effect.
- Weigh long-term flexibility, consistency, resiliency, and performance together; do not adopt
  asynchrony that trades a real correctness/consistency guarantee for marginal throughput.

## 8. Intended file / folder changes

```
MOVE → src/Modules/Underwriting/...Domain/Referrals/
   QuoteReferralOperation.cs, QuoteReferralWorkNote.cs, QuoteReferralFollowUpTask.cs,
   QuoteReferralTimelineEntry.cs, ReferralOperationStatus.cs, ReferralPriority.cs,
   ReferralTimelineEntryType.cs
   (the Quoting enum parameters — CyberRiskTier / QuoteUnderwritingDecision /
    EvidenceReviewDecisionStatus — become string parameters at the seams)

MOVE/NEW → src/Modules/Underwriting/...Application/Referrals/
   Commands/ManageQuoteReferralOperations/  (assign, release, triage, note, task, complete)
   IReferralOperationRepository.cs        (module-owned)
   IReferralOperationProjector.cs         (inbound event-projection port: EnsureCreatedForReferral,
                                           CloseForDecision, RecordEvidence*; idempotent on msg id)
   IReferralOperationsReader.cs           (inbound read port: GetSummaries / GetTimeline)
   (extend) IUnderwritingQuoteContextReader → add ExpiresAtUtc + referred-at for create read-back

NEW/MOVE → src/Modules/Underwriting/...Infrastructure/
   Persistence/UnderwritingDbContext.cs   (+4 DbSets + dedupe table)
   Persistence/QuoteReferral*Configuration.cs  (4 EF configs, moved; FK to Quote already absent)
   Persistence/EfReferralOperationRepository.cs
   Persistence/ReferralOperationProjector.cs   (implements the inbound projection port)
   Persistence/ReferralOperationsReader.cs
   Migrations/<ts>_CreateReferralOperations.cs (adds 4 + dedupe tables to underwriting schema)
   DependencyInjection.cs  (register repository, projector, reader; MediatR already scanned)

LEGACY edits (behavior-preserving):
   OutboxDispatcher.cs                 → also fan out to IReferralOperationProjector
   CreateQuoteCommandHandler.cs        → drop direct CreateDefault/AddReferralOperationAsync
                                          (operation now created by the QuoteGenerated projector)
   Approve/Decline/AdjustHandler.cs    → drop direct GetReferralOperationForUpdate/CloseForDecision
                                          (close now driven by the decision event)
   QuoteEvidenceRequestCommands.cs     → drop the 7 direct operation calls
                                          (evidence projection now driven by evidence events)
   ListQuoteReferralsQueryHandler.cs   → reader.GetSummariesAsync
   GetQuoteReferralTimelineQuery        → reader.GetTimelineAsync (+ keep legacy audit concat)
   IQuoteRepository / EfCoreQuoteRepository → remove the 5 referral-operation methods
   QuoteEvidenceRequestConfiguration.cs → drop HasOne<QuoteReferralOperation> FK (keep id column)
   SubmissionDbContext migration <ts>_DropReferralOperations (drop-and-recreate; no prod data)
   The QuoteGenerated / decision / evidence events: confirm all are captured to the outbox already
   (M17/M18/M25/M26 added them); no new event types needed.

DOCS: this spec; async-and-eventing-conventions.md; project-status.md (M36 status + next-milestone);
   CHANGELOG.md; milestone-36 learnings (post-implementation); architecture/overview.md touch-ups.

UNCHANGED: UnderwritingQuoteReferralsController.cs (still dispatches via MediatR / the module);
   scripts/*, common.ps1 guard, ci.yml (still three DbContexts).
```

## 9. Test plan

- **Move** `QuoteReferralOperationTests` to the module unit-test surface (pure aggregate behavior).
- **New** projector unit/integration tests: create-if-missing idempotency; close on decision event;
  each evidence event → correct timeline/status; re-delivery of the same outbox id is a no-op.
- **Rework** the referral/operations/evidence integration tests for the event-driven wiring: where a
  test creates a referred quote and then asserts operation/timeline/queue state, **pump the
  `IOutboxDispatcher`** (resolved from the host, as `OutboxDispatcherTests` already does) before
  asserting; the underwriter write-command path is covered by the self-healing create-if-missing.
- **New** `UnderwritingDbContext` migration test asserting the four referral tables + dedupe table in
  the `underwriting` schema.
- Architecture ratchet (`ProjectReferenceBoundaryTests`) auto-validates the module boundary; update
  the Infrastructure/Api/Worker consumer rows for the new projector/reader seam.

## 10. Verification before commit (project standard)

- `dotnet build LIAnsureProtect.slnx` — 0 warnings / 0 errors.
- `dotnet test LIAnsureProtect.slnx` — all green (UnitTests + IntegrationTests; 1 PostgreSQL opt-in
  test skipped as usual).
- `dotnet ef migrations has-pending-model-changes` — clean for **all three** contexts
  (Submission, Notifications, Underwriting).
- Full local CI (`scripts/run-local-ci.ps1`) — applies all three contexts' migrations against fresh
  Docker PostgreSQL.
- Deliver via the protected-`main` pull-request flow; squash-merge when CI + Claude review pass.

## 11. Explicitly deferred

- **Evidence** (requests / documents / reviews) carve into the module — its own later milestone; M36
  only moves the *operation timeline projection* of evidence activity, driven by existing events.
- **The underwriting decision** (`ApproveReferral/DeclineReferral/AdjustReferral` +
  `QuoteUnderwritingReview`) — stays on the `Quote` aggregate; the hardest slice, later, and where
  `IUnitOfWork` likely relocates into `Platform.Abstractions`.
- **Integration-event / dispatcher decoupling** (external bus, handler fan-out registry) — M40.
