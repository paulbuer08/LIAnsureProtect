# Chapter 4 — Design Patterns Catalog

Every pattern below is **in the code right now**, with its exact home and the reason it earns its
place. Patterns that are documented-but-planned are listed at the end.

> **Analogy:** patterns are kitchen techniques, not dishes. You don't julienne everything —
> each technique appears only where it beats the simpler alternative.

## Domain & application patterns

| Pattern | Where it lives | Why it's here |
|---|---|---|
| **Aggregate + factory method** | `Submission.CreateDraft`, `Quote`, `QuoteEvidenceRequest`, `NotificationInboxEntry.Create`, … | One controlled door for creation; invariants can't be bypassed (private constructors, private setters). |
| **Domain Events** | `SubmissionSubmittedDomainEvent`, `QuoteGeneratedDomainEvent`, six evidence events, … all implementing `IDomainEvent` | Facts about the past, raised by aggregates, captured transactionally — the seam between modules. |
| **CQRS (practical)** | Every Application folder: `Commands/...` vs `Queries/...` | Writes go through aggregates; reads are shaped, no-tracking projections. One database — CQRS here means *separated models*, not separated stores. |
| **Mediator** | MediatR; controllers send commands/queries | Controllers stay thin HTTP translators; use cases are testable without HTTP. |
| **Decorator (pipeline behavior)** | `ValidationBehavior` in `Application/Common/Behaviors` | Cross-cutting validation wraps every handler without any handler knowing. |
| **Repository** | `ISubmissionRepository`, `IQuoteRepository`, `IEvidenceDocumentRepository`, … → `EfCore*`/`Ef*` implementations | Application states *what* persistence it needs; EF Core details stay in Infrastructure. |
| **Unit of Work** | `IUnitOfWork` → `EfCoreUnitOfWork` | Handlers stage changes, then commit once — and the outbox capture rides that same commit. |
| **Strategy** | `ICyberRatingStrategy` → `BaselineCyberRatingStrategy`, `HighRiskCyberRatingStrategy`; selected by `CyberRatingStrategySelector` | Pricing rules vary by risk profile; new profiles = new strategy class, no `if` forest. |
| **Specification-ish guarded state machine** | Status enums + guard methods (`Quote.ApproveReferral`, `QuoteEvidenceRequest` review transitions) | Illegal transitions throw domain exceptions instead of corrupting state. |

## Reliability patterns (the event spine)

| Pattern | Where | Why |
|---|---|---|
| **Transactional Outbox** | `SubmissionDbContext.SaveChangesAsync` + `ModuleDbContext.CaptureDomainEventsAsync` → `outbox_messages` (legacy + `underwriting` schema) | The business change and the "please tell everyone" memo commit in the **same transaction** — no lost or phantom events. |
| **Polling publisher / dispatcher** | `OutboxDispatcher` driven by `Worker` (5s poll) | Drains all registered `IOutboxSource`s, merge-orders by `CreatedAtUtc`, runs consumers, saves per source. |
| **Competing-consumer-ready retry + poison messages** | `OutboxMessage.MarkPublishFailed` (max 3 attempts, 5-min `NextAttemptAtUtc`, then `FailedAtUtc` parking) | Transient failures retry automatically; poison messages park with the error recorded instead of blocking the queue. |
| **Idempotent consumer / Inbox dedupe** | `NotificationInboxProjector` (keyed on `SourceOutboxMessageId`), `ReferralOperationProjector` + `referral_operation_projected_messages` dedupe table | At-least-once delivery is safe: re-delivery repeats no side effects. |
| **Idempotency key (API)** | `IIdempotencyService` → `EfCoreIdempotencyService`; used by submission create/submit, quote create/accept/bind | A retried POST (timeout, double-click) replays the stored response instead of double-creating. Fingerprint check catches key reuse with a different payload. |
| **Registry (plug-in consumers/mappers)** | `IOutboxMessageConsumer` list + `OutboxMessageMapperRegistry<TOutput>` with per-event mapper classes | New side effects are *registered*, never patched into the dispatcher (M40). Open/closed in practice. |
| **Projection / materialized read model** | Notification inbox entries; referral operations projected from quote/evidence events | Each module owns a read model shaped for its screens, updated by events. |
| **Self-healing projection** | `ReferralOperationProjector` create-if-missing via `IUnderwritingQuoteContextReader` | If an event arrives before its parent projection exists, the projector reconstructs it — eventual consistency without 404s. |

## Integration & resilience patterns

| Pattern | Where | Why |
|---|---|---|
| **Typed HTTP client via `IHttpClientFactory`** | `RatingProviderHttpClient` registered with `AddHttpClient<,>` | Pooled handlers (no socket exhaustion), config in one place. |
| **Retry + Circuit Breaker + Timeout** | `.AddStandardResilienceHandler(...)` on that client | A slow partner degrades one feature, never the whole app (bulkhead-by-configuration). |
| **Anti-corruption seam** | `IRatingProviderClient` port + `RatingProviderRequest/Result` contracts; `IQuoteReferralDecisionService` between Quoting and legacy persistence | External/legacy shapes never leak into Domain. |
| **Fail-closed gate** | `IEvidenceDocumentScanner` + clean-only download/accept gates | A document is untrusted until scanned clean; scanner failure means "no", never "yes". |
| **Federated identity** | Auth0 (OIDC) + JWT bearer validation | Identity is a solved problem — delegate it. |

## Architecture-level patterns

| Pattern | Where | Why |
|---|---|---|
| **Modular monolith + Strangler Fig** | `src/Modules/*` carved from legacy one milestone at a time (M33–M40), behavior-preserving | Incremental, always-green refactor; the legacy core shrinks as contexts move out. |
| **Ports & Adapters + deploy profile** | `Platform.Abstractions` ports; `Platform:Profile` switch | Chapter 3, Idea 3. |
| **Health Endpoint Monitoring** | `/api/v1/health/live` + `/ready` | Orchestrator-grade probes. |
| **Architecture tests as guardrails** | `ProjectReferenceBoundaryTests` + module ratchet | The dependency rules are executable, not tribal knowledge. |

## Documented but deliberately NOT applied (yet)

- **Event Sourcing** — audit needs are met with append-only audit tables (`quote_underwriting_reviews`,
  `quote_evidence_request_reviews`); full ES adds replay/versioning cost with no current payoff.
- **Saga / Process Manager** — flows are short; the referral lifecycle is handled by projections.
  Revisit when multi-step compensation appears (e.g. payment + bind + document issuance).
- **Cache-Aside, Claim-Check, Valet Key, Gatekeeper (WAF)** — arrive with Redis (M44), SNS/SQS
  payloads (M43), S3 presigned URLs (M42), and CloudFront/WAF (M47) respectively.
- **Redux** — client state hasn't justified it; TanStack Query owns server state.
