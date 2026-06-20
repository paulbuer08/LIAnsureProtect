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

Milestone 12 is implemented as `Milestone 12 - Submission Submit And Domain Events Foundation`. It adds an owned submit action for draft submissions through `POST /api/v1/submissions/{submissionId}/submit`, raises `SubmissionSubmittedDomainEvent` from the `Submission` aggregate, and keeps event storage/dispatch deferred to the planned transactional outbox and Worker milestones.

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
- [Pattern Roadmap After Milestone 11](docs/dev/pattern-roadmap-after-milestone-11.md)
- [Milestone 10 Submission List And Detail Foundation Plan](docs/superpowers/plans/2026-06-19-milestone-10-submission-list-and-detail-foundation.md)
- [ADR-005: Application Use Case Patterns](docs/architecture/decision-records/ADR-005-application-use-case-patterns.md)
- [AWS Environments](docs/dev/aws-environments.md)
