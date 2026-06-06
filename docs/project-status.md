# Project Status

This file is the continuity checkpoint for LIAnsureProtect. It should be updated whenever a milestone changes the project direction, architecture, setup, implementation status, or next planned work.

Use this file at the start of a new conversation or coding session before making decisions.

## Current Workspace

- Default project path: `C:\Users\Poy\Documents\LIAnsureProtect`
- Current branch: `codex/milestone-2-backend-foundation`
- Git state: Milestone 1 committed locally as `3d16e8c docs: add project foundation`; Milestone 2 backend foundation is complete on the current branch.
- Current milestone: Milestone 2 - Backend Foundation
- Application code status: backend solution and project structure created; API baseline and root/health endpoint integration tests are in place; frontend application code has not been created yet.

## User Collaboration Rules

- Work milestone by milestone.
- Do not automatically update code without approval.
- Before each milestone, explain the design in simple English.
- Show the intended file/folder changes before implementation.
- Prefer small, understandable code snippets and explain what each part does.
- Keep beginner readability and production-style architecture balanced.
- Update project docs and `CHANGELOG.md` after meaningful changes.
- Add or update a detailed milestone learning notes document for every milestone.
- Use the project files as the source of continuity between conversations.

## Continuity Files To Maintain

Always update these files when the milestone changes the relevant content:

- `README.md`
- `CHANGELOG.md`
- `docs/project-status.md`
- `docs/architecture/*`
- `docs/dev/*`

Add business, security, operations, or deployment docs when a milestone introduces those areas.

Every milestone should also have a learning notes document when meaningful design questions, production tradeoffs, setup lessons, or debugging lessons occur. These notes are mandatory for this project because they preserve the reasoning that led to the final setup.

## Completed Work

### Milestone 1 - Repository And Documentation Foundation

Status: completed and committed locally as `3d16e8c docs: add project foundation`.

Created:

- `global.json`
- `.editorconfig`
- `.gitignore`
- `README.md`
- `CHANGELOG.md`
- `docs/architecture/overview.md`
- `docs/architecture/decision-records/ADR-001-clean-architecture.md`
- `docs/architecture/decision-records/ADR-002-react-frontend.md`
- `docs/architecture/decision-records/ADR-003-postgresql-system-of-record.md`
- `docs/business/cyber-specialty-insurance-overview.md`
- `docs/business/user-roles.md`
- `docs/dev/local-development.md`
- `docs/dev/aws-environments.md`
- `docs/project-status.md`

Verified:

- `.NET SDK` resolves to `10.0.300`.
- `git diff --check` passed before this status file was added.

Known local warning:

- Git warns that it cannot access `C:\Users\Poy/.config/git/ignore`. This is outside the repository and has not blocked project work.

## Approved Product Direction

LIAnsureProtect is a production-style cyber specialty insurance platform for learning and portfolio depth.

The first product scope is a Cyber MVP. The product catalog should stay flexible enough to add Tech E&O, MPL, and Multimedia Liability later.

The app is inspired by specialty insurance workflows, but it must not copy Westfield Specialty branding, proprietary content, forms, product wording, or exact rating logic.

## Primary Roles

- Customer
- Broker
- Underwriter
- ClaimsAdjuster
- Admin

## Target Architecture

The current target is a modular monolith with event-driven workflows.

Simple description:

- Modular monolith: one well-organized application with clear internal departments.
- Event-driven workflows: important business actions publish events, and workers react asynchronously.
- Not full microservices at the start: separate microservices can be extracted later only when scaling, ownership, or deployment independence justifies it.

Recommended interview-style explanation:

```text
I started as a modular monolith with clean boundaries and event-driven workflows using SNS/SQS. That keeps the system easier to build and test early, while allowing high-volume areas like notifications, document processing, claims, or AI review to become separate microservices later if scaling or team ownership requires it.
```

## Backend Direction

Use ASP.NET Core Web API with C# and .NET 10.

Use practical Clean Architecture:

- Domain: business entities, enums, value objects, and domain rules.
- Application: use cases, DTOs, validators, interfaces, and authorization-aware workflows.
- Infrastructure: EF Core, PostgreSQL, Redis, DynamoDB, S3, messaging, and external integrations.
- Api: HTTP endpoints, authentication, authorization, middleware, Swagger/OpenAPI, and health checks.
- Workers: SQS consumers, background processors, notification workers, audit workers, document workers, and AI workers later.
- Tests: unit tests and integration tests.

Important planned interfaces:

- `IDocumentStorageService`: local storage first, S3 later.
- `ICacheService`: Redis-backed cache.
- `IEventPublisher`: transactional outbox first, SNS publisher later.
- `ICurrentUser`: current user id, role, and ownership context.
- `INotificationInboxRepository`: DynamoDB-backed notification inbox.
- `IAiReviewService`: advisory AI summaries and Q&A only.

Public API route prefix:

```text
/api/v1/...
```

## Frontend Direction

Use React 19 with TypeScript and Vite.

Frontend libraries:

- React Router for routing.
- TanStack Query for server state.
- React Hook Form and Zod for forms and validation.
- Zustand only for small client-side state.
- Vitest and React Testing Library for tests.

Do not add Redux initially. Redux Toolkit can be added later only if client-side global state becomes complex enough to justify it.

## Data And Storage Direction

PostgreSQL is the system of record. It is the official filing cabinet for relational business data.

Redis is for cacheable data only. It is a sticky note for things that can be rebuilt, not the official record.

DynamoDB is planned for notification inbox/read-model workloads. It is not the main business database.

S3 is planned for private document storage. Local document storage will come first behind an abstraction.

Do not cache sensitive documents or raw claim details in Redis at the start.

## Cloud Direction

AWS is the target cloud.

Recommended public production shape:

```text
Browser
  -> CloudFront + WAF
  -> S3 frontend files

Browser React app
  -> CloudFront + WAF
  -> ALB
  -> ECS Fargate ASP.NET Core API
  -> RDS PostgreSQL / Redis / S3 / SNS / SQS / DynamoDB
```

Lambda/API Gateway is a second deployment path added after the API shape is stable.

The same core business logic should be shared. Deployment artifacts may differ:

```text
Same source code
  -> ECS artifact: Docker image for long-running ASP.NET Core API
  -> Lambda artifact: Lambda package or Lambda-compatible container image
```

In real production, public users normally use one primary public backend path. For this project, ECS Fargate + ALB is the primary path; Lambda/API Gateway demonstrates serverless capability and may also be used for selected worker-style workloads.

## VPC Direction

Use one VPC per environment, not one VPC per service.

Preferred mature setup:

```text
dev AWS account      -> dev VPC
staging AWS account  -> staging VPC
prod AWS account     -> prod VPC
```

Beginner-friendly alternative:

```text
one AWS account
  -> separate dev/staging/prod VPCs, state, secrets, buckets, queues, and databases
```

Typical VPC layout:

```text
VPC
  -> public subnets: ALB, NAT Gateway
  -> private app subnets: ECS tasks, Lambda functions that need private resources
  -> private database subnets: RDS PostgreSQL, ElastiCache Redis
```

S3, DynamoDB, SNS, SQS, Secrets Manager, and Parameter Store are managed AWS services accessed through AWS APIs. They are not placed behind an ALB.

## CloudFront, WAF, And Origin Lockdown

CloudFront is the CDN and public front door for the frontend. It serves React static assets quickly from edge locations and can route `/api/*` requests to an API origin.

WAF should start at CloudFront.

Recommended first production security shape:

```text
CloudFront + WAF
  -> S3 frontend origin
  -> ALB or API Gateway API origin
```

Origin lockdown is not the same as CORS.

- CORS is a browser permission rule.
- Origin lockdown is infrastructure access control that prevents users from bypassing CloudFront.

Planned origin lockdown:

- S3 frontend bucket: block public access and use CloudFront Origin Access Control.
- ALB origin: allow CloudFront origin-facing traffic where practical, use CloudFront custom origin header, and have ALB listener rules reject missing/invalid origin headers.
- API Gateway origin later: use custom domain, disable default endpoint where applicable, and validate a CloudFront origin header in middleware or an authorizer if needed.

## Security And Compliance Defaults

- Encrypt data at rest.
- Use private S3 buckets.
- Use least-privilege IAM.
- Keep secrets out of Git.
- Use HTTPS outside local development.
- Use WAF for public entry points.
- Audit sensitive actions such as document upload/download and underwriting/claims decisions.
- Use RDS backups, PITR, and Multi-AZ for production.
- Use S3 lifecycle rules for document retention and cost control.
- Keep dev, staging, and production isolated.

## AI Direction

AI/RAG is intentionally late.

AI features should be advisory only:

- Document summarization.
- Underwriting assistant.
- Claims assistant.

AI must not approve, deny, bind, issue, or close insurance decisions.

Planned RAG flow:

```text
Document uploaded
  -> text extraction
  -> chunking
  -> embeddings
  -> PostgreSQL + pgvector
  -> RAG answer with citations
```

## Milestone 2 Completion

### Milestone 2 - Backend Foundation

Status: complete.

Intent:

- Create the ASP.NET Core solution and project structure.
- Add Clean Architecture project references.
- Add API startup with health checks, ProblemDetails, Swagger/OpenAPI, configuration pattern, and structured logging.
- Add test projects.
- Keep business features out of this milestone unless needed for a smoke test.

Created:

- `LIAnsureProtect.slnx` exists.
- Production projects are under `src`: Domain, Application, Infrastructure, Api, and Worker.
- Test projects are under `tests`: UnitTests and IntegrationTests.
- Project references follow the Clean Architecture dependency direction.
- Weather template demo files and empty class library templates were removed.
- API startup includes OpenAPI, ProblemDetails, health checks, HTTPS redirection, authorization middleware, a simple root status endpoint, and `/api/v1/health`.
- IntegrationTests contains real root status and health endpoint tests using `WebApplicationFactory`.
- UnitTests currently contains no tests because no Domain or Application behavior exists yet.

Milestone 2 decisions to remember:

- Keep business API routes under `/api/v1/...` from the beginning.
- Use formal API versioning later before real breaking API changes or when multiple live API versions are needed.
- Keep OpenAPI development-only for now.
- Later, generate separate OpenAPI documents when needed for versions, public/internal audiences, or frontend/backend groupings.
- Later, protect OpenAPI documentation with role-based authorization before exposing it outside local development.
- Do not add authentication, CORS, database schema, frontend code, or cloud infrastructure in Milestone 2 unless explicitly approved.
- Use built-in ASP.NET Core logging first; for AWS ECS/Fargate, prefer console logs shipped to CloudWatch before adding Serilog or CloudWatch-specific logging packages.
- Keep test-host settings local to integration tests unless the value changes by environment.
- Prefer explicit public route expectations in integration tests so tests protect the API contract.

Milestone 2 verification:

- Visual Studio build and tests passed on the local machine.
- Command-line `dotnet build LIAnsureProtect.slnx --no-restore` passed.
- Command-line `dotnet test LIAnsureProtect.slnx --no-build` passed; IntegrationTests passed with root status and health endpoint coverage, and UnitTests built but has no tests yet by design.
- `git diff --check` passed with only normal CRLF warnings.

## Next Planned Milestone

Milestone 3 should be planned in the next session before implementation starts.

Candidate Milestone 3 directions:

- Add shared Application and Infrastructure dependency-registration extension methods.
- Add a first small architecture-boundary test if it adds real value.
- Start the first Domain/Application business slice only after agreeing on scope.

Do not start authentication, database schema, React frontend, or cloud infrastructure until the relevant milestone is explicitly approved.

## Open Local Setup Items

Known from earlier environment checks:

- .NET 10 is installed.
- Docker is installed, but Docker config access produced a permission warning.
- Node.js/npm is not currently usable from this environment.
- AWS CLI is not currently available from this environment.
- Terraform is not currently available from this environment.

These are not blockers for backend foundation, but Node/npm is required before the React frontend milestone, and AWS CLI/Terraform are required before deployment milestones.
