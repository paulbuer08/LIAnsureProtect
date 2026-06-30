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
  + the six `QuoteEvidenceRequest*DomainEvent`s, all into the module + `underwriting` schema.
- Use cases that move fully into the module (no document dependency): **create**, **cancel**,
  **follow-up**, the **owner-list** query, and the queue **evidence-summary** read.
- The module outbox + the source-agnostic (merge-ordered) dispatcher.

**Stay legacy in M37 but mutate the now-module-owned request through one inbound port (§4):** the
document-coupled use cases — **respond** (text + upload), **replacement-upload**, **accept**, and
**record-review-decision** (these gate on / snapshot clean-document state and return documents), plus the
two **downloads**. They keep orchestrating documents legacy-side and pass the request-state change — and
any document counts, as **primitives** — to the module via the port. So the request aggregate is only
ever mutated on the module context (and raises all six events to the module outbox), yet
`QuoteEvidenceDocument` and the clean-document gate stay put with **no outbound document-read port**.

> Why this line: drawing the boundary at *document-coupling* (rather than owner-vs-underwriter) means
> the heavily document-dependent review/accept/respond handlers don't need to reach back into the module
> for the request *or* forward into legacy for documents from the module — each side keeps what it owns
> and the single inbound port carries primitives across. This seam dissolves entirely in M38.

**Deferred to M38 (documents):** `QuoteEvidenceDocument`, `IDocumentStorageService`,
`IEvidenceDocumentScanner`, and the document storage/scanning/download internals — moving documents into
the module then removes the M37 respond/accept/review seam.

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
  IOutboxMessageView   — Id, Type, Payload, CreatedAtUtc + MarkProcessed / MarkPublishSucceeded /
                         MarkPublishFailed

Legacy Infrastructure:   SubmissionOutboxSource : IOutboxSource (wraps SubmissionDbContext.OutboxMessages)
Module Infrastructure:   UnderwritingOutboxSource : IOutboxSource (wraps UnderwritingDbContext outbox)

OutboxDispatcher: injects IEnumerable<IOutboxSource>; collects each source's pending batch, then
  MERGE-SORTS the combined set by CreatedAtUtc and processes globally in that order — running the
  existing mappers (OutboxNotificationMapper → notifications, OutboxReferralOperationMapper → referral
  projector) keyed on Type+Payload, publishing, and marking via the message's owning source
  (per-source SaveChanges, no shared transaction; same idempotent-ordered model as M33/M36).
```

**Cross-source ordering matters (and is double-backstopped).** The referral operation projector consumes
quote/decision events (legacy outbox) *and* evidence events (now the module outbox). In M36 a single
outbox's `CreatedAtUtc` order guaranteed evidence projected before the decision closed the operation;
two independently-drained sources would break that and the closed-operation guard could silently drop a
late evidence timeline entry. Merge-sorting the combined pending set by `CreatedAtUtc` restores the
global order. Belt and suspenders: M36's projector is already resilient here — **create-if-missing**
self-heal tolerates evidence-before-create, and the closed-operation **guard** tolerates the reverse — so
correctness does not hinge on perfect ordering, but merge-sort keeps the common-case timeline faithful.

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

## 4. The request/review carve + the inbound seam

**Move into the module** (`Modules/Underwriting/...Application/Evidence`): the document-free commands
(**create**, **cancel**, **follow-up**) raise their events on the module context (→ module outbox), and
the reads move behind a module read port:
- `IEvidenceRequestsReader.GetOwnerRequestsAsync(ownerUserId)` — the owner list.
- `IEvidenceRequestsReader.GetSummariesAsync(quoteIds)` — the queue's per-quote evidence summary
  (today computed in `ListQuoteReferralsQueryHandler` from legacy evidence; now read from the module,
  mirroring `IReferralOperationsReader`).

**The inbound seam (documents + their handlers stay legacy in M37).** The legacy respond /
replacement-upload / accept / review-decision handlers keep orchestrating documents (store, scan, the
clean-document gate, the count snapshot) in the legacy context, but every **request-state change** goes
through one inbound port, so the request is only ever mutated module-side and raises all six events to
the module outbox:

```
Module Application: IEvidenceRequestWriter   (every parameter is a primitive)
  RecordResponseAsync(evidenceRequestId, ownerUserId, respondent + response + attachment-metadata, at)
      → request.RecordResponse(...)                                      raises Responded
  RecordSupplementalResponseAsync(evidenceRequestId, ownerUserId, ..., at)
      → replacement-upload: resets the current review decision to NotReviewed, preserving audit
  AcceptAsync(evidenceRequestId, reviewedByUserId, reason, documentCount, cleanDocumentCount, at)
      → request.Accept(...) + writes the moved QuoteEvidenceRequestReview audit   raises Accepted
  RecordReviewDecisionAsync(evidenceRequestId, decision, reason, guidance, reviewedByUserId,
                            documentCount, cleanDocumentCount, at)
      → request.RecordReviewDecision(...) + writes the review audit   raises RemediationRequired
```

The legacy review/accept handler keeps the document work it already does — load documents, run the
clean-document gate, compute `documentCount`/`cleanDocumentCount` — then passes those **counts as
primitives** to the port, which mutates the module request and writes the now-module-owned
`QuoteEvidenceRequestReview` audit. The response DTO is assembled legacy-side from the port's returned
request snapshot + the legacy documents. **No outbound document-read port is needed** — documents never
leave legacy and the module never reads them.

Atomicity: the legacy document save and the module request/port save are **not** one transaction (e.g.
respond stores documents legacy, then calls the port). If the second save fails the owner re-acts; the
brief inconsistency is acceptable for evidence and disappears in M38. `IEvidenceRequestWriter` takes
**primitives** only — the evidence enums move *with* the aggregate, so no legacy handler references a
moved module enum; the port validates ownership/state and maps internally.

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
       Commands (document-free): Create / Cancel / FollowUp; Query: ListOwnerEvidenceRequests
       IEvidenceRequestRepository, IEvidenceRequestsReader,
       IEvidenceRequestWriter (inbound port: respond / supplemental / accept / review-decision)
NEW  Modules/Underwriting/...Infrastructure:
       EfEvidenceRequestRepository, EvidenceRequestsReader, EvidenceRequestWriter,
       QuoteEvidenceRequest/Review EF configs (underwriting schema), UnderwritingOutboxSource,
       CaptureDomainEventsAsync override, outbox + evidence migration
EDIT OutboxDispatcher → source-agnostic (IEnumerable<IOutboxSource>, merge-sort by CreatedAtUtc);
       mappers take Type/Payload and reference the module evidence event types (new ratchet edge
       Infrastructure → Modules.Underwriting.Domain); register SubmissionOutboxSource + UnderwritingOutboxSource
EDIT legacy QuoteEvidenceRequestCommands.cs → KEEP respond / replacement-upload / accept /
       review-decision / downloads (document-coupled); each calls IEvidenceRequestWriter for the
       request-state change (passing primitive counts); remove the moved create/cancel/follow-up
       commands and the request/review repo methods from IQuoteRepository/EfCoreQuoteRepository
EDIT moved request aggregate → drop the QuoteReferralOperationId property + its Create parameter (the
       M36 vestigial correlation is gone now); references quote/submission by id only
EDIT legacy QuoteEvidenceDocumentConfiguration → drop the document → quote_evidence_requests FK (the
       request table is leaving; documents reference it by id only until M38)
EDIT ListQuoteReferralsQueryHandler → evidence summary via IEvidenceRequestsReader
EDIT EvidenceRequestsController + UnderwritingQuoteReferralsController → dispatch create/cancel/follow-up/
       owner-list to the module; respond/accept/review/downloads stay legacy (now call the inbound port)
DROP evidence→quotes + evidence→submissions FKs; document→request FK; the quote_referral_operation_id
       column (it leaves with the dropped request table)
MIGRATIONS: CreateUnderwritingOutbox + CreateEvidenceRequests (UnderwritingDbContext);
       DropEvidenceRequests (SubmissionDbContext — drops quote_evidence_requests +
       quote_evidence_request_reviews, taking the vestigial column with them, and drops the
       document→request FK) — drop-and-recreate, no production data
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
  sources drained in one dispatch). Plus a **cross-source ordering** test: an earlier evidence event
  (module outbox) and a later decision event (legacy outbox) pending together are processed in
  `CreatedAtUtc` order, so the evidence timeline entry is recorded before the operation closes.
- Rework the evidence endpoint/integration tests for the two-source wiring; the **respond** and
  **accept/review-decision** tests exercise the `IEvidenceRequestWriter` seam (documents + the
  clean-document gate stay legacy, the request + review-audit mutate module-side, counts passed as
  primitives); pump the dispatcher where eventual consistency applies (as M36 established).
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
