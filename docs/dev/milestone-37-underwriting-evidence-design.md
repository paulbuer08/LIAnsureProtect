# Milestone 37 - Underwriting Evidence (foundation + request/review carve) — Design Spec

> Status: **approved-for-planning** (design-first; written before any code).
> Branch: `feat/milestone-37-underwriting-evidence`.
> Third slice of the Underwriting carve (after M35 AI review, M36 referral operations). This is the
> *largest* sub-context, so it is split: M37 stands up the module-as-event-source foundation and carves
> the evidence **request + review** aggregates; **documents** (storage/scanning/downloads) defer to M38.

## 1. Goal & scope

Lift the evidence-**request** lifecycle out of the legacy `Quotes` context into the Underwriting module
+ the `underwriting` schema, and — because evidence is the first module aggregate that *raises* domain
events — introduce the **module-owned outbox + multi-source dispatch** foundation it requires. Finish
the M36 decoupling (drop the vestigial `quote_referral_operation_id` column + the evidence→`quotes`/
`submissions` cross-context FKs).

**In M37:**
- Aggregates: `QuoteEvidenceRequest` (root) + `QuoteEvidenceRequestReview` (audit) + the evidence enums
  + the six `QuoteEvidenceRequest*DomainEvent`s.
- Use cases (non-document): create, accept, record-review-decision, cancel, follow-up, owner-list.
- The module outbox + the source-agnostic dispatcher.
- Reads: owner-list + the queue's evidence summary via a module read port.

**Deferred to M38 (documents):** `QuoteEvidenceDocument`, `IDocumentStorageService`,
`IEvidenceDocumentScanner`, and the document use cases — respond-with-upload, replacement-upload, owner
download, underwriting download. In M37 these stay **legacy** and reach the now-module-owned request
through one inbound port (§4).

**Deferred further:** full dispatcher decoupling / integration events → M40 (M37 lays its foundation);
the underwriting **decision** slice (`Approve/Decline/Adjust` + `QuoteUnderwritingReview`) remains the
hardest, latest slice and is where `IUnitOfWork` likely relocates into `Platform.Abstractions`.

## 2. The module-outbox foundation (the novel piece)

`ModuleDbContext.CaptureDomainEventsAsync` is a **no-op** today — fine when modules were event *sinks*
(M35 AI review, M36 referral operations raised no events). Evidence is the first event *source*, so the
module must persist its events transactionally with the business change. The module **cannot** write to
the legacy `SubmissionDbContext` outbox (a module→legacy boundary violation), so it gets its own.

```
Platform (shared kernel):
  ModuleOutboxMessage           — reusable outbox row (Id, Type, Payload, OccurredAtUtc, CreatedAtUtc,
                                  ProcessedAtUtc + publish-retry fields + Mark* methods), with a
                                  FromDomainEvent(...) factory (mirrors the legacy OutboxMessage shape).
  ModuleOutboxMessageConfiguration — reusable EF config a module applies in its own schema.

UnderwritingDbContext:
  overrides CaptureDomainEventsAsync → adds ModuleOutboxMessage rows to underwriting.outbox_messages
  in the SAME transaction as the evidence save (the transactional-outbox guarantee).
```

**Decision (approved): the module gets its own `underwriting.outbox_messages`; the legacy
`SubmissionDbContext` outbox is left untouched.** Promoting/sharing one outbox table is cleaner
long-term but a bigger, riskier refactor of legacy — we converge at M40. There are temporarily two
outbox message *types* (legacy `OutboxMessage`, Platform `ModuleOutboxMessage`) behind a common dispatch
interface (§3); the small duplication is the price of leaving legacy untouched this milestone.

## 3. Multi-source dispatch (M40 groundwork, brought forward)

The `OutboxDispatcher` (legacy Infrastructure) drains one outbox today. It becomes **source-agnostic**:

```
Platform.Abstractions:
  IOutboxSource        — GetPendingAsync(batchSize, nowUtc, ct) → IReadOnlyList<IOutboxMessageView>;
                         SaveChangesAsync(ct)
  IOutboxMessageView   — Id, Type, Payload + MarkProcessed / MarkPublishSucceeded / MarkPublishFailed

Legacy Infrastructure:   SubmissionOutboxSource : IOutboxSource (wraps SubmissionDbContext.OutboxMessages)
Module Infrastructure:   UnderwritingOutboxSource : IOutboxSource (wraps UnderwritingDbContext outbox)

OutboxDispatcher: injects IEnumerable<IOutboxSource>; for each source drains its pending messages,
  runs the existing mappers (OutboxNotificationMapper → notifications, OutboxReferralOperationMapper →
  referral projector) keyed on Type+Payload, publishes, and marks — per-source SaveChanges, no shared
  transaction (same idempotent-ordered model as M33/M36).
```

`IOutboxSource` lives in `Platform.Abstractions` so the dispatcher (legacy Infra) and the module source
(module Infra) can both reference it without a module→legacy or legacy→module-Infra dependency. The
mappers change from taking the concrete legacy `OutboxMessage` to taking `Type`/`Payload` (or the view),
and reference the **module** evidence event types (the legacy event types are deleted in this carve).

**Boundary note — centralized mapping stays in legacy for M37 (a transitional ratchet edge).** The
event→notification mapping produces a `NotificationMessage` (Notifications module Application type), so
it *cannot* live inside the Underwriting module (a module→module reference is forbidden). It stays in
the legacy/host `OutboxNotificationMapper` + `OutboxReferralOperationMapper`, which therefore must
reference the moved evidence event types — adding a new allowed edge **`LIAnsureProtect.Infrastructure →
Modules.Underwriting.Domain`** in `ProjectReferenceBoundaryTests` (the dispatcher host is the one place
that can see every module). This mirrors how legacy Infra already references the legacy `Domain` quote
events. **M40** removes this edge by moving mapping into per-module handlers/registries; M37 keeps the
centralized mapper deliberately.

```
 evidence action (module) → underwriting.outbox_messages ┐
 quote/policy action (legacy) → public.outbox_messages   ┴─► OutboxDispatcher (drains both)
                                                               ├─► notification projector + publisher
                                                               └─► referral operation projector
```

## 4. The request/review carve + the respond seam

The six **non-document** use cases move into `Modules/Underwriting/...Application/Evidence` and raise
their events on the module context (→ module outbox). The reads move behind a module read port:
- `IEvidenceRequestsReader.GetOwnerRequestsAsync(ownerUserId)` — the owner list.
- `IEvidenceRequestsReader.GetSummariesAsync(quoteIds)` — the queue's per-quote evidence summary
  (today computed in `ListQuoteReferralsQueryHandler` from legacy evidence; now read from the module,
  mirroring `IReferralOperationsReader`).

**The respond seam (documents stay legacy in M37).** The legacy respond / replacement-upload / download
handlers keep storing + scanning documents in the legacy context, but mutate the now-module-owned
request through one inbound port:

```
Module Application: IEvidenceRequestWriter
  RecordResponseAsync(evidenceRequestId, ownerUserId, respondent + response + attachment-metadata, at)
      → loads the module request (owner-scoped), request.RecordResponse(...), saves (raises Responded)
  RecordSupplementalResponseAsync(evidenceRequestId, ownerUserId, ..., at)
      → replacement-upload path: resets the current review decision to NotReviewed, preserving audit
```

Ordering in the legacy respond handler: validate uploads → store + scan documents (legacy) → call
`RecordResponseAsync` (module mutates the request + raises the event). The two saves (legacy documents,
module request) are **not atomic** — if the document store fails the request stays `Open`; if the port
call fails after storing, the documents are orphaned and the owner re-responds. This brief inconsistency
is acceptable for an evidence response and disappears in M38 when documents join the module. All six
evidence events therefore end up sourced from the module outbox, consistently.

`IEvidenceRequestWriter` takes **primitives** at the boundary (ids, strings, the attachment-metadata
values) — the evidence enums move *with* the aggregate into the module, so the legacy document handlers
never reference a moved module enum; the port validates ownership and maps to the aggregate internally.

## 5. Consistency & boundaries

No new consistency model: evidence events were already eventually consistent through the outbox; they
only change **source** (module, not legacy). The module references quote/submission **by id only** —
the evidence→`quotes` and evidence→`submissions` FKs are dropped, and the vestigial
`quote_referral_operation_id` column is removed. The legacy `Application → Modules.Underwriting.Application`
reference edge already exists (M36); the architecture ratchet (`ProjectReferenceBoundaryTests`) is
updated for any new consumer rows the same way M35/M36 did.

## 6. Intended file / folder changes

```
NEW  Platform/.../Outbox: ModuleOutboxMessage, ModuleOutboxMessageConfiguration, FromDomainEvent helper
NEW  Platform.Abstractions: IOutboxSource, IOutboxMessageView
MOVE Modules/Underwriting/...Domain/Evidence: QuoteEvidenceRequest, QuoteEvidenceRequestReview, enums
       (EvidenceRequestCategory, EvidenceRequestStatus, EvidenceReviewDecisionStatus), the 6 events
MOVE Modules/Underwriting/...Application/Evidence:
       Commands: Create/Accept/RecordReviewDecision/Cancel/FollowUp; Query: ListOwnerEvidenceRequests
       IEvidenceRequestRepository, IEvidenceRequestsReader, IEvidenceRequestWriter (inbound respond port)
NEW  Modules/Underwriting/...Infrastructure:
       EfEvidenceRequestRepository, EvidenceRequestsReader, EvidenceRequestWriter,
       QuoteEvidenceRequest/Review EF configs (underwriting schema), UnderwritingOutboxSource,
       CaptureDomainEventsAsync override, outbox + evidence migration
EDIT OutboxDispatcher → source-agnostic (IEnumerable<IOutboxSource>); mappers take Type/Payload and
       reference module evidence event types; register SubmissionOutboxSource + UnderwritingOutboxSource
EDIT legacy QuoteEvidenceRequestCommands.cs → keep document use cases (respond/upload/downloads); they
       call IEvidenceRequestWriter for the request-state change; remove the moved request/review repo
       methods from IQuoteRepository/EfCoreQuoteRepository
EDIT ListQuoteReferralsQueryHandler → evidence summary via IEvidenceRequestsReader
EDIT EvidenceRequestsController + UnderwritingQuoteReferralsController → dispatch moved use cases to the
       module; document actions unchanged (still legacy)
DROP evidence→quotes + evidence→submissions FKs and the quote_referral_operation_id column
MIGRATIONS: CreateEvidenceRequests + CreateUnderwritingOutbox (UnderwritingDbContext);
       DropEvidenceRequests + DropQuoteReferralOperationId (SubmissionDbContext) — drop-and-recreate,
       no production data
DOCS: this spec; learnings (post-impl); project-status M37 + M38 handoff; CHANGELOG; README; overview
UNCHANGED: scripts/guard/CI (still three DbContexts — the module outbox is a table in UnderwritingDbContext)
```

## 7. Test plan

- Move the request/review aggregate unit tests to the module surface (the evidence enums move *with*
  the aggregate, so these are mostly namespace changes — unlike M36, few string-param conversions; the
  seam ports/mappers use primitives at the boundary).
- New: `ModuleOutboxMessage` capture test (a module evidence event lands an `underwriting.outbox_messages`
  row in the same transaction); `UnderwritingOutboxSource` pending/mark test.
- New: source-agnostic dispatcher test — an evidence event in the **module** outbox is projected to
  notifications + the referral operation, and a quote event in the **legacy** outbox still works (both
  sources drained in one dispatch).
- Rework the evidence endpoint/integration tests for the two-source wiring; the respond test exercises
  the `IEvidenceRequestWriter` seam (documents legacy, request module); pump the dispatcher where
  eventual consistency applies (as M36 established).
- New migration tests: evidence tables + outbox in `underwriting`; drop migration on `submission`.
- Architecture ratchet updated for any new reference rows.

## 8. Verification (before commit — project standard)

- `dotnet build LIAnsureProtect.slnx` — 0 warnings / 0 errors.
- `dotnet test` — all green (1 PostgreSQL opt-in test skipped).
- `dotnet ef migrations has-pending-model-changes` — clean for all three contexts.
- Full local CI (`scripts/run-local-ci.ps1`) against fresh Docker PostgreSQL.
- Delivered via the protected-`main` PR flow; no AI attribution on commits/PRs.

## 9. Explicitly deferred

- **Evidence documents** (entity, storage, scanning, replacement, downloads) → **M38**, which also
  removes the respond seam by moving documents into the module.
- **Full dispatcher decoupling / integration events** (external bus, handler registry) → **M40**; M37
  delivers the multi-source foundation it builds on.
- **The underwriting decision** → later; where `IUnitOfWork` likely relocates into `Platform.Abstractions`.
