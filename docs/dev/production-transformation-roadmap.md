# Production Transformation Roadmap

This is the approved multi-milestone program to evolve LIAnsureProtect into a realistic, fully-working,
**AWS-deployable production application** — while keeping the **Cyber** product as the only implemented line
for now. It is an **evolution, not a rewrite**: the existing foundation (Clean Architecture, CQRS, outbox,
idempotency, Auth0, React, CI/CD + security) is kept and refactored one PR-gated, always-green milestone at a time.

> Companion: the GitHub/CI/PR workflow this program follows is in
> [github-repository-and-automation.md](github-repository-and-automation.md). Deep per-concept docs land under
> `docs/concepts/`, per-service docs under `docs/aws/`, patterns under `docs/patterns/`, security under
> `docs/security/`, and runbooks under `docs/runbooks/` as each milestone is built.

## Architecture style: Clean Architecture **and** Modular Monolith

- **Clean Architecture** = layering *inside* each module (Domain → Application → Infrastructure → host edge).
- **Modular Monolith** = many **bounded-context modules** in **one deployable**, each with its own database
  schema and public **contracts**; modules talk only via contracts/events, never each other's tables/internals.
- **Ports & Adapters (Hexagonal)** = every infrastructure concern is an interface (port) with swappable adapters
  (Local vs AWS) chosen by config — this powers the **Local ⇄ AWS deploy switch**.

> Analogy: the app is one office building (single deployable). Each floor is a department (bounded context) with
> its own filing room (schema). Departments exchange memos (contracts/events), never rummage in each other's
> cabinets. Plumbing/power (storage, messaging, identity) are standardized sockets (ports) you plug local or
> cloud equipment into (adapters).

## Deployment profiles — the Local ⇄ AWS switch

A `Platform:Profile` setting (config + env var; secrets via User Secrets locally / Secrets Manager in cloud)
selects adapters at the composition root. The **same container image** runs everywhere; only config + which IaC
you apply differs (docker-compose / local k8s for Local; Terraform + EKS for AWS). **Auth0 stays** and works in
cloud; **Cognito** is an optional, config-selectable alternative.

| Concern (port) | Local adapter | AWS adapter |
|---|---|---|
| Identity (IdP) | Auth0 / local test | Auth0 *or* Cognito |
| Database | Postgres (Docker) | Aurora PostgreSQL (pgvector) |
| Object storage | local filesystem | S3 (+ SSE-KMS, presigned URLs) |
| Messaging | in-process / LocalStack | SNS + SQS (+ DLQ) + EventBridge |
| Cache | in-memory / local Redis | ElastiCache (Redis) |
| Secrets/config | User Secrets / appsettings | Secrets Manager / SSM Parameter Store |

## Bounded contexts (target)

Each becomes a Clean-layered module with its own PostgreSQL schema and contracts. Boundaries enforced by
module-architecture tests (extending `ProjectReferenceBoundaryTests`); no cross-module FKs (reference by id +
events); in-process module event bus now → outbox → SNS/SQS later.

`Platform` (shared kernel: outbox/idempotency/events/messaging/audit/tenancy/observability) · `Identity & Access` ·
`Accounts/Companies` · `Product Catalog` (multi-product-capable, **Cyber only**) · `Submissions/Intake` ·
`Rating` · `Underwriting` (referral ops, evidence, AI) · `Quoting` · `Policy` · `Claims` · `Documents` ·
`Notifications` (personal **+ team inbox**).

## Cross-cutting workstreams (acceptance criteria on every milestone)

- **Documentation & Concepts Handbook** — every concept/tool/AWS-service/pattern documented richly (simple
  English, diagram, analogy, how/why) under the `docs/` taxonomy above.
- **Design-patterns catalog** — apply where valuable and document all: Unit of Work, Repository, CQRS, Mediator,
  Outbox/Idempotency/Inbox, REPR, Saga/Process Manager, Specification, Strategy, Decorator (pipeline behaviors),
  Result; cloud patterns: Strangler Fig, Cache-Aside, Competing Consumers, Claim-Check, **Valet Key** (presigned
  URLs), Circuit Breaker/Retry/Bulkhead, Gatekeeper (WAF), Federated Identity, Health Endpoint Monitoring,
  Materialized View. Event Sourcing is documented but applied selectively (audit-heavy contexts only).
- **Security-by-design** — defense in depth, least-privilege IAM (+ GitHub OIDC, no static keys), encryption
  everywhere (TLS + KMS), secrets in a vault, resource-based AuthZ at every layer, OWASP ASVS alignment,
  supply-chain (SBOM, signed/pinned images & actions), WAF + rate limiting + security headers, Macie for
  sensitive PII, step-up MFA for high-risk actions only — balanced so it never chokes performance/HA/UX.
- **Analytics foundation now (Glue later)** — stable integration-event contracts + the outbox as the event
  spine + an optional S3 event-archive sink, so Glue/Athena/QuickSight slot in cleanly in a later milestone.

## AWS target (provisioned later, Terraform, guided-manual, `destroy`-able)

```text
 Browser ─► CloudFront + WAF ─► [ S3 static React ]   and  ─► ALB/Ingress ─► EKS (API pods, Fargate profile)
                                                                                   │
   Cognito or Auth0 (OIDC)  ◄── JWT ──────────────────────────────────────────────┤
                                                                                   ▼
   EKS Worker pods ◄─ SQS ◄─ SNS ◄─ transactional outbox ◄─ Aurora PostgreSQL (pgvector)
        │                 ▲                                   ▲          │
        ▼                 │ EventBridge (routing + cron)      │ ElastiCache  └─► (opt) S3 event archive → [Glue/Athena later]
   Lambda (S3-triggered doc scan/OCR, scheduled jobs)         │
        ▼                                                     │
   S3 (docs, SSE-KMS, Object Lock) · Secrets Manager · KMS · CloudTrail · GuardDuty · Macie · Inspector · Config
   Observability: CloudWatch + OpenTelemetry (→ X-Ray/Datadog)
```

- **Compute — EKS** with **Fargate profiles** (no node management) for API + Worker; **Lambda** for event/glue
  functions. *Honest note:* EKS adds real complexity + a ~$0.10/hr control-plane baseline — chosen for the
  learning goal, run cost-controlled and torn down between sessions; ECS Fargate is the lighter fallback.
- **Edge:** CloudFront + WAF + ACM; API Gateway reserved for a future partner API.
- **Data/storage:** Aurora PostgreSQL Serverless v2 (pgvector), ElastiCache, S3 (+KMS, Object Lock).
- **Messaging/orchestration:** SNS/SQS (+DLQ), EventBridge, Step Functions.
- **Identity:** Auth0 default, Cognito optional. **IaC:** Terraform (remote state, per-env modules).
- **Glue** is deferred to a later analytics milestone (it is ETL/analytics, not app runtime).

## Roadmap (one PR-gated, always-green milestone at a time)

**Phase 0 — Upfront structural refactor (behavior-preserving):**
- **M32** — Platform & module skeleton + deploy-profile switch (`src/Modules/<Context>/…`, `src/Platform`,
  schema-per-module pattern, ports/adapters composition, module-architecture tests).
- **M33** — Notifications module carve (personal inbox read model moved behind module contracts).
- **M34** — Notifications team inbox (first feature built inside a carved module).
- **M35** — Underwriting module: advisory AI review carve.
- **M36** — Underwriting referral operations carve.
- **M37** — Underwriting evidence request/review carve + module outbox + source-agnostic dispatcher foundation.
- **M38** — Underwriting evidence document carve (document metadata, scanner, upload/download/replacement gates).
- **M39** — Quoting decision boundary: prepare/begin the `Quote`/`QuoteUnderwritingReview` carve instead of forcing
  final approve/decline/adjust into Underwriting.
- **M40** — Dispatcher integration-event decoupling / mapper registry, replacing transitional host-side
  dispatcher-owned event mapping with registered consumers and mapper registries.

**Phase 1 — Production cross-cutting (local/docker, LocalStack):**
- **M41** Observability (implemented: correlation, health/readiness, native dispatcher logs/traces/metrics) ·
  **M42** Documents → S3 (**implemented**: `S3DocumentStorageService` behind the storage port, SSE-KMS,
  LocalStack-tested; Valet-Key presigned URLs, provisioning, and S3-triggered scan deferred to M46/M47) ·
  **M43** Real async messaging (**implemented**: `SnsNotificationPublisher` behind the notification
  publisher port, versioned envelope → SNS → SQS + DLQ, LocalStack-tested; always-on SQS consumer and
  optional S3 event archive deferred) · **M44** Caching + rate limiting.

### Fully-baked next-milestone plans (detailed 2026-07-02 after the post-M41 solidification audit)

**M42 — Documents To S3.** Goal: swap the private document byte-store adapter from local
filesystem to S3 **without touching any business flow** (the Chapter 8 evidence flow diagram in
`docs/encyclopedia/` must stay valid). Scope: an `S3DocumentStorageService` implementing the
existing `Platform.Abstractions.Documents` contracts, selected by `Platform:Profile=Aws` (the
today-fail-fast branch becomes real); LocalStack-backed integration tests so the adapter is
tested without an AWS bill; SSE-KMS encryption settings and private-bucket assumptions expressed
in configuration; Valet-Key (presigned URL) download path prepared behind the same download
endpoints (feature-flagged — API streaming stays the default until CloudFront exists); document
metadata, scan trust state, and clean-only gates unchanged. Acceptance: all existing evidence
tests green against both adapters; encyclopedia Chapter 2/8 updated.

**M43 — Real async messaging.** Goal: the outbox stops being only-polled and starts publishing
integration events to SNS→SQS (LocalStack locally), with the Worker consuming from SQS; DLQ +
redrive documented; per-event contract records versioned (the analytics foundation); optional S3
event-archive sink behind a flag. The dispatcher/consumer/mapper-registry shape from M40 is the
extension point — publishing becomes one more registered consumer, and in-process projection
remains for the module seams that need read-your-writes. Acceptance: at-least-once + idempotent
consumers proven by tests that kill the worker mid-batch; encyclopedia Chapter 10 updated.

**M44 — Caching + rate limiting.** Goal: `ICacheService` port + Redis cache-aside for
rebuildable reads (reference data, queue summaries — never documents/PII), ASP.NET Core rate
limiting on the public API (fixed-window per user + stricter on unsafe POSTs), and security
headers middleware. Acceptance: cache invalidation tests, 429 behavior tests, encyclopedia
Chapters 2/3/11 updated.

### Tooling decisions recorded (2026-07-02)

- **`IHttpClientFactory` / typed clients + `Microsoft.Extensions.Http.Resilience`** — already the
  project standard since M19 (`RatingProviderHttpClient`); every future outbound HTTP integration
  (S3 presign checks, webhook callers, partner APIs) must use a typed client with the standard
  resilience handler. No retrofit needed.
- **Ansible** — evaluated alongside Terraform and **deferred**: the target runtime is
  EKS/Fargate containers plus managed services, so there are no long-lived VMs for configuration
  management to own; Terraform (+ Helm charts applied in M47) covers provisioning and workload
  config. Revisit only if EC2-based components (self-managed runners, bastions) enter the
  architecture — then Ansible would own OS-level config while Terraform keeps provisioning.

**Phase 2 — AWS infrastructure (Terraform, guided-manual):**
- **M45** TF foundation (state, IAM+OIDC, VPC, KMS, Secrets Manager) · **M46** Data+storage (Aurora, ElastiCache, S3) ·
  **M47** Compute+edge (ECR, EKS+Fargate, ALB/Ingress, CloudFront+WAF; Lambda+EventBridge+Step Functions) ·
  **M48** Identity (Cognito option) · **M49** CD pipeline (Actions→ECR→EKS blue/green, gated migrations, smoke tests) ·
  **M50** Security & compliance (GuardDuty/Security Hub/Config/Inspector/Macie/CloudTrail/WAF/rotation).

**Phase 3 — Later (after a working production deployment):** other specialty products, AI/RAG (Bedrock + pgvector),
analytics (S3 data-lake + Glue + Athena/QuickSight), partner API, payments/e-sign.

## Guardrails

- **No big-bang** — strangler refactor; one context per PR; CI + module-architecture tests green throughout;
  behavior-preserving moves first, new features/domains in separate PRs.
- **Cost control** — all AWS infra is Phase 2+, provisioned manually and `terraform destroy`-able so nothing bills idle.
- Security and documentation are acceptance criteria on every milestone, not afterthoughts.
