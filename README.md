# LIAnsureProtect

LIAnsureProtect is a production-style cyber specialty insurance platform built for learning and portfolio depth. It is inspired by specialty insurance workflows, but it is not affiliated with or copied from any insurer.

The first product scope is a Cyber MVP. The system will support customer and broker submissions, insured company profiles, cyber questionnaires, document handling, risk scoring, underwriting review, quotes, policies, claims, notifications, observability, and later AI-assisted document review.

## Target Stack

- Backend: ASP.NET Core Web API with C# and .NET 10
- Architecture: practical Clean Architecture
- Application patterns: practical CQRS with MediatR and FluentValidation
- Database: PostgreSQL with Entity Framework Core and pgvector-ready local development
- Frontend: React 19, TypeScript, Vite
- Local platform: Docker Compose for application dependencies
- Cloud target: AWS
- AWS services over time: ECS Fargate, ALB, Lambda, API Gateway, RDS PostgreSQL, RDS Proxy, S3, SQS, SNS, DynamoDB, ElastiCache Redis, CloudWatch, WAF, Secrets Manager, Parameter Store, Terraform

## Build Style

This project is built milestone by milestone. Each milestone should be small enough to understand, test, document, and debug before moving on.

Before implementation, document the design. After implementation, update docs and the changelog.

## Current Status

Milestone 3 is dependency registration and architecture guards. The backend has shared Application and Infrastructure dependency-registration extension methods, API and Worker startup use those shared methods, and tests protect the current project-reference boundaries.

Milestone 4 is complete as `Milestone 4 - Application Use Case Foundation`. It introduced practical CQRS, MediatR, FluentValidation, a validation pipeline behavior, and the first submission intake slice exposed through `POST /api/v1/submissions`.

Milestone 5 is implemented locally as `Milestone 5 - Persistence Foundation`. It replaces the temporary in-memory submission repository with EF Core persistence, adds a PostgreSQL-backed Infrastructure repository, introduces Unit of Work as the Application commit boundary, and adds Docker Compose plus EF Core migrations for a containerized PostgreSQL/pgvector dependency.

Milestone 6 is complete as `Milestone 6 - Authentication Foundation`. It adds JWT bearer authentication, policy-based authorization, a current-user abstraction, and protects `POST /api/v1/submissions` behind the `Submissions.Create` policy for `Customer`, `Broker`, and `Admin` roles.

Milestone 7 is complete as `Milestone 7 - Identity Provider Integration`. It connects the provider-neutral JWT foundation to Auth0 for the first external identity provider, starts with manual token testing, stores the tenant-specific Auth0 authority through ASP.NET Core User Secrets, and records later security-hardening goals such as permission strings, ownership policies, JWE evaluation, sender-constrained tokens, step-up MFA, and refresh-token/session security.

Milestone 8 is complete as `Milestone 8 - Frontend Login And Session Foundation`. It adds the first React/Vite frontend, Auth0 Authorization Code with PKCE login, guarded dashboard routing, access-token retrieval for the LIAnsureProtect API audience, a protected API smoke-test button, local development CORS support, focused frontend tests, and a verified browser-to-API-to-PostgreSQL smoke path.

Milestone 9 is implemented as `Milestone 9 - Submission Intake UI Foundation`. It replaces the dashboard-only hard-coded smoke submission with a protected `/submissions/new` workflow using React Hook Form, Zod, TanStack Query, and the existing Auth0 access-token API call path. The submission intake frontend is organized as a feature-owned vertical slice under `src/LIAnsureProtect.Web/src/features/submissions` so future submission workflows can grow without turning route pages into large mixed files.

Milestone 10 is implemented as `Milestone 10 - Submission List And Detail Foundation`. It adds the first protected submission read workflow after creation: `GET /api/v1/submissions`, `GET /api/v1/submissions/{submissionId}`, Application query handlers, EF Core no-tracking LINQ reads, protected `/submissions` and `/submissions/:submissionId` frontend routes, and TanStack Query read states. It uses REPR-style request/endpoint/response thinking and the existing vertical-slice structure without replacing the current controller-based API.

Milestone 11 is implemented as `Milestone 11 - Submission Ownership Foundation`. It adds the first real ownership rule for submissions: each new submission stores the authenticated owner user id, and protected list/detail reads are scoped to that same signed-in user before the project expands into richer submission navigation graphs.

Milestone 12 is complete as `Milestone 12 - Submission Submit And Domain Events Foundation`. It adds an owned submit action for draft submissions through `POST /api/v1/submissions/{submissionId}/submit`, raises `SubmissionSubmittedDomainEvent` from the `Submission` aggregate, and keeps event storage/dispatch deferred to the planned transactional outbox and Worker milestones.

Milestone 13 is complete as `Milestone 13 - Transactional Outbox Foundation`. It persists submitted-domain events into an `outbox_messages` table in the same PostgreSQL database transaction as the submission status update, keeping dispatch, SNS/SQS, email, retries, and idempotency for later milestones.

Milestone 14 is complete as `Milestone 14 - Outbox Dispatcher Foundation`. It adds the first Worker-side dispatcher path that reads pending PostgreSQL `outbox_messages` rows and marks them processed locally. This keeps the milestone focused on the polling/processing loop before adding SNS/SQS, email, notification inboxes, full retry policy, circuit breakers, idempotency keys, quote generation, or underwriting queues.

Milestone 15 is complete as `Milestone 15 - Idempotent Submission Actions Foundation`. It adds PostgreSQL-backed `Idempotency-Key` handling for the current protected write endpoints: `POST /api/v1/submissions` and `POST /api/v1/submissions/{submissionId}/submit`. Matching retries return the stored response without rerunning the write, while unsafe key reuse returns `409 Conflict`.

Milestone 16 is complete as `Milestone 16 - Idempotency Operational Hardening Foundation`. It adds the first operational hardening slice for idempotency by deleting expired completed `idempotency_records` from the Worker, keeping abandoned `InProgress` recovery and required-key policy decisions documented for later hardening slices.

Milestone 17 is complete as `Milestone 17 - Cyber Rating And Quote Foundation`. It adds the first realistic local cyber rating slice with synthetic actuarial-style factors, owner-scoped quote creation for submitted submissions, PostgreSQL quote persistence, `QuoteGeneratedDomainEvent` outbox capture, and idempotent quote creation through `POST /api/v1/submissions/{submissionId}/quotes`.

Milestone 18 is complete as `Milestone 18 - Underwriting Referral Foundation`. It builds on Milestone 17's `Referred` quote state with an underwriter referral queue, `Quotes.Underwrite` authorization, approve/decline/adjust review actions, PostgreSQL review audit history, and `QuoteUnderwritingDecisionRecordedDomainEvent` outbox capture without introducing external provider calls, policy binding, notification delivery, or AI yet.

Milestone 19 is complete as `Milestone 19 - External Rating Provider Adapter And Resilience Foundation`. It adds an Application-owned external rating provider boundary, an Infrastructure typed `HttpClient` adapter with `Microsoft.Extensions.Http.Resilience` retry and circuit-breaker behavior, a simulated provider-shaped market indication, and PostgreSQL `quote_rating_provider_attempts` audit persistence while keeping local rating and underwriting referral behavior authoritative.

Milestone 20 is complete as `Milestone 20 - Quote Acceptance And Policy Binding Foundation`. It adds quote acceptance attestations, durable bound policies, simulated binding acknowledgement audit rows, idempotent accept/bind POST actions, and `PolicyBoundDomainEvent` outbox capture while keeping real payment collection, production policy documents, notification delivery, real e-signature, real carrier binding APIs, and advisory AI out of scope until later milestones.

Milestone 21 is complete as `Milestone 21 - Notification And Outbox Publishing Foundation`. It puts the existing transactional outbox to practical use through an Application-owned notification publisher boundary, provider-shaped local publishing for important quote and policy workflow events, explicit publish retry/failure metadata on outbox rows, and `QuoteAcceptedDomainEvent` capture while keeping production SNS/SQS, email/SMS delivery, inboxes, preferences, webhooks, and notification templates out of scope.

Milestone 22 is complete as `Milestone 22 - AI Underwriting Assistant Foundation`. It adds an underwriter-only advisory AI review endpoint for referred cyber quotes, an Application-owned AI review boundary, a local simulated AI review provider, PostgreSQL `ai_underwriting_reviews` audit persistence, structured cyber underwriting review packets, prompt/schema/input hash audit fields, and tests proving AI output remains advisory and cannot change quote, policy, premium, underwriting decision, acceptance, or binding state.

Milestone 23 is implemented as `Milestone 23 - Underwriting Workbench UI Foundation`. It adds a protected `/underwriting/quote-referrals` React workbench for underwriters to triage referred quotes, inspect referral reasons and subjectivities, request advisory AI review, and record manual approve/decline/adjust decisions through the existing backend endpoints without changing underwriting authority rules.

Milestone 24 is implemented as `Milestone 24 - Underwriting Referral Operations Foundation`. It adds durable referral operations state for referred quotes: self-assignment, priority, SLA due dates, workflow status, append-only work notes, internal follow-up tasks, and an audit timeline exposed in the existing underwriting workbench. AI remains advisory-only and final approve/decline/adjust authority remains human-owned.

Milestone 25 is implemented as `Milestone 25 - Underwriting Evidence Request Foundation`. It adds PostgreSQL-backed evidence requests for referred quotes: underwriters can request cyber-control evidence by category, customer/broker owners can respond with text and safe placeholder attachment metadata, underwriters can accept or cancel requests, and the workbench tracks open/responded evidence activity without adding full document storage, OCR, RAG, notification inboxes, or autonomous AI review.

Milestone 26 is implemented as `Milestone 26 - Evidence Request Notification and Follow-up Foundation`. It adds evidence request lifecycle domain events, local outbox-backed notification messages for evidence created/responded/accepted/cancelled/follow-up activity, a manual underwriter follow-up reminder action, and due/overdue evidence indicators in both the underwriting workbench and owner evidence page without adding production email/SMS delivery, notification inboxes, document storage, OCR, RAG, scheduled reminder automation, or messaging threads yet.

Milestone 27 is implemented locally as `Milestone 27 - Evidence Document Storage Foundation`. It replaces evidence response attachment metadata placeholders with private local evidence document upload/download behavior: customer/broker owners can upload up to five evidence files per response, PostgreSQL stores safe metadata in `quote_evidence_documents`, file bytes stay outside the database behind an Application-owned document storage boundary, and authorized owners and underwriters download documents through private API routes while production S3, public URLs, virus scanning, OCR, embeddings, RAG, and autonomous AI document review stay out of scope.

Milestone 28 is implemented locally as `Milestone 28 - Evidence Document Security Screening Foundation`. It adds a local quarantine-style security screening workflow for uploaded evidence documents: newly uploaded files are scanned through an Application-owned scanner boundary, scan status and SHA-256 metadata are persisted on `quote_evidence_documents`, only clean documents are downloadable or acceptable for underwriting evidence review, rejected or failed documents stay visible for audit, and owners can append replacement evidence without deleting the original rejected file. Full local CI passed with artifact `TestResults\local-ci-20260623-160248.zip`.

Milestone 29 is complete as `Milestone 29 - Evidence Review Decision Audit Foundation`. It adds human-owned evidence sufficiency review after document security screening: underwriters can record `Satisfied`, `Insufficient`, or `NeedsClarification` decisions with rationale, owner remediation guidance, reviewer metadata, referral timeline entries, and append-only audit rows that snapshot the trusted document count at review time. Owners can submit supplemental evidence after unfavorable review decisions, while pending, rejected, or failed documents remain blocked from trusted review. OCR, autonomous AI evidence review, embeddings/RAG, legal hold, policy binding, and final quote approval automation remain out of scope. Full local CI passed with artifact `TestResults\local-ci-20260623-173225.zip`.

Milestone 30 is complete as `Milestone 30 - Evidence Review Outcome Notification Foundation`. It adds an unfavorable evidence review outcome notification path: `Insufficient` and `NeedsClarification` decisions raise an outbox-backed remediation-required domain event that maps to a customer/broker local notification with safe action-oriented attributes. `Satisfied` evidence remains on the existing accepted-evidence notification path. Production email/SMS delivery, notification preferences, messaging threads, OCR, AI evidence review, policy binding, and final quote approval automation remain out of scope. Full local CI passed with artifact `TestResults\local-ci-20260623-185058.zip`.

Milestone 31 is implemented as `Milestone 31 - Notification Inbox Read Model Foundation`. It adds an owner-facing notification inbox so customers/brokers can read the notifications the system already publishes: a PostgreSQL `notification_inbox_entries` read model behind an Application-owned `INotificationInboxRepository`, written by the outbox dispatcher beside the existing publish step (only for `customer-or-broker` messages, idempotent on the source outbox message id), owner-scoped `GET /api/v1/notifications` (list + unread count) and `POST /api/v1/notifications/{id}/read` behind a new `Notifications.Read` policy, and a React `features/notifications` slice (list page with unread count + mark-read, plus a dashboard link). Underwriter/binding team-inbox audiences, DynamoDB, production email/SMS delivery, a notification preference center, real-time push, per-type templates, and messaging threads remain out of scope. See [Milestone 31 Handoff & Planning](docs/dev/milestone-31-notification-inbox-read-model-foundation-handoff.md) and [Learnings](docs/dev/milestone-31-notification-inbox-read-model-foundation-learnings.md).

Milestone 32 is implemented as `Milestone 32 - Platform & Module Skeleton + Local⇄AWS Deploy Switch`, the first milestone of the production-transformation program. It is behavior-preserving: it adds the `src/Platform` shared kernel (`LIAnsureProtect.Platform.Abstractions` ports + `LIAnsureProtect.Platform` adapters), a `src/Modules/` placeholder for future bounded-context modules, the config-driven `Platform:Profile` Local⇄AWS deploy switch (proven first on document storage), the schema-per-module `ModuleDbContext` template, and a module-boundary architecture-test ratchet. It deliberately does **not** split the existing `SubmissionDbContext` or move any table — the first real context carve (Notifications) lands in Milestone 33 and the team inbox follows in Milestone 34. The concepts are documented under [docs/concepts/](docs/concepts/README.md). See [Milestone 32 Learnings](docs/dev/milestone-32-platform-module-skeleton-learnings.md).

Milestone 33 is implemented as `Milestone 33 - Notifications Module`, the first real bounded-context carve and behavior-preserving. The notification inbox moves out of the legacy layered projects into `src/Modules/Notifications/{Domain,Application,Infrastructure}` with its own `NotificationsDbContext` owning a dedicated `notifications` PostgreSQL schema. The outbox dispatcher now feeds the module through an `INotificationProjector` port using idempotent ordered projection (no distributed transaction), and `ICurrentUser` moves into the Platform shared kernel. Because there are now two `DbContext`s, the dev scripts and CI apply migrations per `--context`. The team inbox (a new feature) is the next milestone. See [Milestone 33 Learnings](docs/dev/milestone-33-notifications-module-learnings.md).

Milestone 34 is implemented as `Milestone 34 - Notifications Team Inbox`, the first feature built natively inside a carved module. It persists the `underwriting-operations`/`binding-operations` audiences the dispatcher previously dropped as shared `team_notification_entries`, with **per-user read receipts** created lazily on mark-read (team membership comes from the caller's role claim, so no user directory is needed). The `/api/v1/notifications` list merges personal + team notifications (each tagged with `scope`/`audience`), mark-read handles either, the `Notifications.Read` policy now includes Underwriter, and the notifications page gains All/Personal/Team filter tabs plus a Team badge. See [Milestone 34 Learnings](docs/dev/milestone-34-notifications-team-inbox-learnings.md).

Milestone 35 is implemented as `Milestone 35 - Underwriting Module: AI Review`, the first slice of the (multi-milestone) Underwriting carve and behavior-preserving. The advisory AI underwriting review moves out of the legacy `Quotes` namespace into `src/Modules/Underwriting/{Domain,Application,Infrastructure}` with its own `UnderwritingDbContext` owning a dedicated `underwriting` PostgreSQL schema. Because the underwriting *decision* lives on the `Quote` aggregate (Quoting context), the module reads a read-only quote snapshot through a new `IUnderwritingQuoteContextReader` port (implemented on the legacy side) and never mutates the quote — so "AI cannot make an insurance decision" is now a structural guarantee. There are now three `DbContext`s; scripts and CI apply each with `--context`. See [Milestone 35 Learnings](docs/dev/milestone-35-underwriting-ai-review-module-learnings.md).

Milestone 36 is implemented as `Milestone 36 - Underwriting Referral Operations`, the second slice of the Underwriting carve and the most entangled so far. The `QuoteReferralOperation` aggregate (referral queue/SLA + work notes + follow-up tasks + timeline) moves into the Underwriting module and the `underwriting` schema — its tables join the existing `UnderwritingDbContext` (no fourth context). The cross-context hand-offs are **event-driven**: the outbox dispatcher now also feeds a module `IReferralOperationProjector` (idempotent on the source outbox-message id, create-if-missing) that reacts to existing quote/decision/evidence events to create, close, and project activity onto the operation — so creation/closure are eventually consistent (mitigated so there's no user-visible gap). The underwriter's own actions stay synchronous module commands; the queue/timeline reads stay legacy behind an `IReferralOperationsReader` port. The evidence→operation cross-schema FK is dropped (reference by id only; evidence carves in M37). This milestone also establishes [Async / Await and Eventing Conventions](docs/dev/async-and-eventing-conventions.md) as a global best practice. See [Milestone 36 Design](docs/dev/milestone-36-underwriting-referral-operations-design.md) and [Milestone 36 Learnings](docs/dev/milestone-36-underwriting-referral-operations-learnings.md).

Milestone 37 is implemented as `Milestone 37 - Underwriting Evidence`, the third Underwriting carve slice. Evidence **requests and reviews** now live in the Underwriting module and `underwriting` schema, with module-owned request/review aggregates, commands, readers, repositories, and outbox events. The dispatcher can drain both the legacy and module outbox sources in `CreatedAtUtc` order, so evidence notifications and referral-operation projection keep their event ordering as the source moves. Evidence documents deliberately remained legacy for one milestone through the temporary `IEvidenceRequestWriter` seam, then moved in Milestone 38. See [Milestone 37 Design](docs/dev/milestone-37-underwriting-evidence-design.md) and [Milestone 37 Learnings](docs/dev/milestone-37-underwriting-evidence-learnings.md).

Milestone 38 is implemented as `Milestone 38 - Underwriting Evidence Documents`, the fourth Underwriting carve slice. It completes the M37 temporary seam by moving generic private document storage contracts to Platform.Abstractions, moving the evidence scanner port/local scanner and document aggregate into Underwriting, moving document metadata into `underwriting.quote_evidence_documents`, and making upload/replacement/download/accept/review/owner-list document workflows module Application handlers. Public routes and React behavior stayed stable, while `IQuoteRepository` is quote-focused again and the temporary `IEvidenceRequestWriter` seam is gone. See [Milestone 38 Design](docs/dev/milestone-38-underwriting-evidence-documents-design.md), [Milestone 38 Plan](docs/superpowers/plans/2026-07-01-milestone-38-underwriting-evidence-documents.md), and [Milestone 38 Learnings](docs/dev/milestone-38-underwriting-evidence-documents-learnings.md).

Milestone 39 is started as `Milestone 39 - Quoting Decision Boundary`. It should make final quote referral decision authority explicit as a Quoting boundary rather than forcing approve/decline/adjust behavior into Underwriting. The first design keeps public routes stable, keeps Underwriting as the operational referral/evidence context, and prepares the smallest safe path around `Quote`, `QuoteUnderwritingReview`, rating attempts, and final decision commands. See [Milestone 39 Design](docs/dev/milestone-39-quoting-decision-boundary-design.md) and [Milestone 39 Plan](docs/superpowers/plans/2026-07-02-milestone-39-quoting-decision-boundary.md).

## Local Run

Run a fresh dependency stack, apply migrations, build, and start the API from the repository root:

```powershell
.\scripts\dev-up.ps1
```

That script resets the local Docker Compose dependency stack by default, removes the local PostgreSQL volume, starts PostgreSQL/pgvector, applies EF Core migrations through the repo-local `dotnet-ef` tool manifest, and runs the API.

For setup without tests or starting the API, run:

```powershell
.\scripts\setup-dev.ps1
```

To include tests in the setup run:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

Run the combined local CI path, including backend setup/tests/smoke checks and frontend install/build/lint/test checks:

```powershell
.\scripts\run-local-ci.ps1
```

## Documentation

- [Project Status](docs/project-status.md)
- [Architecture Overview](docs/architecture/overview.md)
- [Cyber Specialty Insurance Overview](docs/business/cyber-specialty-insurance-overview.md)
- [User Roles](docs/business/user-roles.md)
- [Run The App](docs/dev/run-the-app.md)
- [Local Development](docs/dev/local-development.md)
- [Dependency Management](docs/dev/dependency-management.md)
- [CI/CD Flow](docs/dev/ci-cd-flow.md)
- [Milestone Documentation Practice](docs/dev/milestone-documentation-practice.md)
- [Async / Await and Eventing Conventions (Global Best Practice)](docs/dev/async-and-eventing-conventions.md)
- [Milestone 2 Backend Foundation Learnings](docs/dev/milestone-2-backend-foundation-learnings.md)
- [Milestone 3 Dependency Registration And Architecture Guards Learnings](docs/dev/milestone-3-dependency-registration-and-architecture-guards-learnings.md)
- [Milestone 4 Application Use Case Foundation Learnings](docs/dev/milestone-4-application-use-case-foundation-learnings.md)
- [Milestone 5 Persistence Foundation Learnings](docs/dev/milestone-5-persistence-foundation-learnings.md)
- [Milestone 6 Authentication Foundation Learnings](docs/dev/milestone-6-authentication-foundation-learnings.md)
- [Milestone 7 Identity Provider Integration Learnings](docs/dev/milestone-7-identity-provider-integration-learnings.md)
- [Milestone 8 Frontend Login And Session Foundation Learnings](docs/dev/milestone-8-frontend-login-and-session-foundation-learnings.md)
- [Milestone 9 Submission Intake UI Foundation Learnings](docs/dev/milestone-9-submission-intake-ui-foundation-learnings.md)
- [Milestone 10 Submission List And Detail Foundation Learnings](docs/dev/milestone-10-submission-list-and-detail-foundation-learnings.md)
- [Milestone 11 Submission Ownership Foundation Learnings](docs/dev/milestone-11-submission-ownership-foundation-learnings.md)
- [Milestone 12 Submission Submit And Domain Events Foundation Learnings](docs/dev/milestone-12-submission-submit-and-domain-events-foundation-learnings.md)
- [Milestone 13 Transactional Outbox Foundation Learnings](docs/dev/milestone-13-transactional-outbox-foundation-learnings.md)
- [Milestone 14 Outbox Dispatcher Foundation Learnings](docs/dev/milestone-14-outbox-dispatcher-foundation-learnings.md)
- [Milestone 15 Idempotent Submission Actions Foundation Learnings](docs/dev/milestone-15-idempotent-submission-actions-foundation-learnings.md)
- [Milestone 16 Idempotency Operational Hardening Foundation Learnings](docs/dev/milestone-16-idempotency-operational-hardening-foundation-learnings.md)
- [Milestone 17 Cyber Rating And Quote Foundation Learnings](docs/dev/milestone-17-cyber-rating-and-quote-foundation-learnings.md)
- [Milestone 18 Underwriting Referral Foundation Learnings](docs/dev/milestone-18-underwriting-referral-foundation-learnings.md)
- [Milestone 19 External Rating Provider Adapter And Resilience Foundation Learnings](docs/dev/milestone-19-external-rating-provider-adapter-and-resilience-foundation-learnings.md)
- [Milestone 20 Quote Acceptance And Policy Binding Foundation Learnings](docs/dev/milestone-20-quote-acceptance-and-policy-binding-foundation-learnings.md)
- [Milestone 21 Notification And Outbox Publishing Foundation Learnings](docs/dev/milestone-21-notification-and-outbox-publishing-foundation-learnings.md)
- [Milestone 22 AI Underwriting Assistant Foundation Learnings](docs/dev/milestone-22-ai-underwriting-assistant-foundation-learnings.md)
- [Milestone 23 Underwriting Workbench UI Foundation Learnings](docs/dev/milestone-23-underwriting-workbench-ui-foundation-learnings.md)
- [Milestone 24 Underwriting Referral Operations Foundation Learnings](docs/dev/milestone-24-underwriting-referral-operations-foundation-learnings.md)
- [Milestone 25 Underwriting Evidence Request Foundation Learnings](docs/dev/milestone-25-underwriting-evidence-request-foundation-learnings.md)
- [Milestone 26 Evidence Request Notification and Follow-up Foundation Learnings](docs/dev/milestone-26-evidence-request-notification-follow-up-foundation-learnings.md)
- [Milestone 27 Evidence Document Storage Foundation Learnings](docs/dev/milestone-27-evidence-document-storage-foundation-learnings.md)
- [Milestone 28 Evidence Document Security Screening Foundation Learnings](docs/dev/milestone-28-evidence-document-security-screening-foundation-learnings.md)
- [Milestone 29 Evidence Review Decision Audit Foundation Learnings](docs/dev/milestone-29-evidence-review-decision-audit-foundation-learnings.md)
- [Milestone 30 Evidence Review Outcome Notification Foundation Learnings](docs/dev/milestone-30-evidence-review-outcome-notification-foundation-learnings.md)
- [Milestone 31 Notification Inbox Read Model Foundation Handoff & Planning](docs/dev/milestone-31-notification-inbox-read-model-foundation-handoff.md)
- [Milestone 31 Notification Inbox Read Model Foundation Learnings](docs/dev/milestone-31-notification-inbox-read-model-foundation-learnings.md)
- [Milestone 32 Platform & Module Skeleton Learnings](docs/dev/milestone-32-platform-module-skeleton-learnings.md)
- [Milestone 33 Notifications Module Learnings](docs/dev/milestone-33-notifications-module-learnings.md)
- [Milestone 34 Notifications Team Inbox Learnings](docs/dev/milestone-34-notifications-team-inbox-learnings.md)
- [Milestone 35 Underwriting AI Review Module Learnings](docs/dev/milestone-35-underwriting-ai-review-module-learnings.md)
- [Milestone 36 Underwriting Referral Operations Design](docs/dev/milestone-36-underwriting-referral-operations-design.md)
- [Milestone 36 Underwriting Referral Operations Learnings](docs/dev/milestone-36-underwriting-referral-operations-learnings.md)
- [Milestone 37 Underwriting Evidence Design](docs/dev/milestone-37-underwriting-evidence-design.md)
- [Milestone 37 Underwriting Evidence Learnings](docs/dev/milestone-37-underwriting-evidence-learnings.md)
- [Milestone 38 Underwriting Evidence Documents Design](docs/dev/milestone-38-underwriting-evidence-documents-design.md)
- [Milestone 38 Underwriting Evidence Documents Plan](docs/superpowers/plans/2026-07-01-milestone-38-underwriting-evidence-documents.md)
- [Milestone 38 Underwriting Evidence Documents Learnings](docs/dev/milestone-38-underwriting-evidence-documents-learnings.md)
- [Milestone 39 Quoting Decision Boundary Design](docs/dev/milestone-39-quoting-decision-boundary-design.md)
- [Milestone 39 Quoting Decision Boundary Plan](docs/superpowers/plans/2026-07-02-milestone-39-quoting-decision-boundary.md)
- [GitHub Repository, CI/CD, and Automation](docs/dev/github-repository-and-automation.md)
- [Production Transformation Roadmap](docs/dev/production-transformation-roadmap.md)
- [Pattern Roadmap After Milestone 11](docs/dev/pattern-roadmap-after-milestone-11.md)
- [Milestone 10 Submission List And Detail Foundation Plan](docs/superpowers/plans/2026-06-19-milestone-10-submission-list-and-detail-foundation.md)
- [ADR-005: Application Use Case Patterns](docs/architecture/decision-records/ADR-005-application-use-case-patterns.md)
- [AWS Environments](docs/dev/aws-environments.md)
