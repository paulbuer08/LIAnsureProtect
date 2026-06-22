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
- [Pattern Roadmap After Milestone 11](docs/dev/pattern-roadmap-after-milestone-11.md)
- [Milestone 10 Submission List And Detail Foundation Plan](docs/superpowers/plans/2026-06-19-milestone-10-submission-list-and-detail-foundation.md)
- [ADR-005: Application Use Case Patterns](docs/architecture/decision-records/ADR-005-application-use-case-patterns.md)
- [AWS Environments](docs/dev/aws-environments.md)
