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
- **M33** — Notifications module + **team inbox** (per-user read receipts; the deferred feature, in its proper home).
- **M34** — Underwriting module carved from `Quotes`.
- **M35** — Rating + Quoting + Policy modules/schemas.
- **M36** — Accounts/Companies + Product Catalog (Cyber-only data).
- **M37** — Multi-tenancy (org + territory scoping) + AuthZ hardening.
- **M38** — Claims module skeleton (FNOL + lifecycle).

**Phase 1 — Production cross-cutting (local/docker, LocalStack):**
- **M39** Observability · **M40** Real async messaging (outbox→SNS/SQS, DLQ) + integration-event contracts (+ optional S3 archive) ·
  **M41** Caching + rate limiting · **M42** Documents → S3 (Valet Key, KMS, scan pipeline, retention/legal hold).

**Phase 2 — AWS infrastructure (Terraform, guided-manual):**
- **M43** TF foundation (state, IAM+OIDC, VPC, KMS, Secrets Manager) · **M44** Data+storage (Aurora, ElastiCache, S3) ·
  **M45** Compute+edge (ECR, EKS+Fargate, ALB/Ingress, CloudFront+WAF; Lambda+EventBridge+Step Functions) ·
  **M46** Identity (Cognito option) · **M47** CD pipeline (Actions→ECR→EKS blue/green, gated migrations, smoke tests) ·
  **M48** Security & compliance (GuardDuty/Security Hub/Config/Inspector/Macie/CloudTrail/WAF/rotation).

**Phase 3 — Later (after a working production deployment):** other specialty products, AI/RAG (Bedrock + pgvector),
analytics (S3 data-lake + Glue + Athena/QuickSight), partner API, payments/e-sign.

## Guardrails

- **No big-bang** — strangler refactor; one context per PR; CI + module-architecture tests green throughout;
  behavior-preserving moves first, new features/domains in separate PRs.
- **Cost control** — all AWS infra is Phase 2+, provisioned manually and `terraform destroy`-able so nothing bills idle.
- Security and documentation are acceptance criteria on every milestone, not afterthoughts.
