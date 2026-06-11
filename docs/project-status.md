# Project Status

This file is the continuity checkpoint for LIAnsureProtect. It should be updated whenever a milestone changes the project direction, architecture, setup, implementation status, or next planned work.

Use this file at the start of a new conversation or coding session before making decisions.

## Current Workspace

- Default project path: `C:\Users\Poy\Documents\LIAnsureProtect`
- Current branch: `codex/milestone-2-backend-foundation`
- Git state: Milestone 1 committed locally as `3d16e8c docs: add project foundation`; Milestone 2 committed locally as `f36a8aa feat: add backend foundation`; Milestone 3 committed locally as `bb4b547 feat: add dependency registration and architecture guards`; Milestone 4 planning committed locally as `dab62d0 docs: add application use case foundation plan`; Milestone 4 implementation committed locally as `fe8c27d feat: add application use case foundation`.
- Current milestone: Milestone 5 - Persistence Foundation
- Application code status: backend solution and project structure created; API baseline and root/health endpoint integration tests are in place; shared Application and Infrastructure dependency-registration methods have been added; architecture-boundary tests now protect the current project-reference direction; Milestone 4 contains the first submission intake slice using `POST /api/v1/submissions`, MediatR, FluentValidation, a validation pipeline behavior, `ISubmissionRepository`, and Moq-backed handler tests; Milestone 5 replaces temporary in-memory submission storage with EF Core/PostgreSQL persistence, `SubmissionDbContext`, explicit submission mapping, a PostgreSQL-backed repository, Unit of Work, Docker Compose PostgreSQL/pgvector dependency setup, the first EF Core migration, centralized NuGet package versions, and an opt-in PostgreSQL-backed integration test; frontend application code has not been created yet.

## User Collaboration Rules

- Work milestone by milestone.
- Do not automatically update code without approval.
- Before each milestone, explain the design in simple English.
- Show the intended file/folder changes before implementation.
- Prefer small, understandable code snippets and explain what each part does.
- Keep beginner readability and production-style architecture balanced.
- Update project docs and `CHANGELOG.md` after meaningful changes.
- Add or update a detailed milestone learning notes document for every milestone.
- Use consistent milestone titles in the format `Milestone N - Title Case Name`.
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

At the end of each milestone session, create the next milestone session/context window with a concise handoff prompt after the milestone is verified and committed.

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

Approved Application use case direction:

- Use practical CQRS in the Application layer starting in Milestone 4 - Application Use Case Foundation.
- Use MediatR to dispatch commands and queries to their handlers.
- Use FluentValidation to validate command/query request models before handlers execute.
- Use Moq in unit tests only when handlers depend on interfaces that need test doubles.
- Add domain events and a transactional outbox in a later milestone for reliable asynchronous workflows.
- Do not use event sourcing as the default persistence model. Consider event sourcing later only for selected workflows if replayable history has clear value.

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

pgvector is expected to extend PostgreSQL later for AI/RAG embeddings. Do not introduce a separate vector database by default.

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

## Recent Completed Work

Milestone 3 - Dependency Registration And Architecture Guards is complete.

Intent:

- Add shared Application and Infrastructure dependency-registration extension methods.
- Wire API and Worker startup through the shared registration methods.
- Add a first small architecture-boundary test because it now adds real value.
- Do not start the first Domain/Application business slice yet.

Created:

- `src/LIAnsureProtect.Application/DependencyInjection.cs`
- `src/LIAnsureProtect.Infrastructure/DependencyInjection.cs`
- API startup calls `AddApplication()` and `AddInfrastructure()`.
- Worker startup calls `AddApplication()` and `AddInfrastructure()`.
- `tests/LIAnsureProtect.IntegrationTests/DependencyRegistrationTests.cs`
- `tests/LIAnsureProtect.UnitTests/Architecture/ProjectReferenceBoundaryTests.cs`
- `docs/dev/milestone-3-dependency-registration-and-architecture-guards-learnings.md`

Milestone 3 decisions to remember:

- `AddApplication()` and `AddInfrastructure()` are intentionally small now. They are stable setup doors for future services, not a reason to invent fake services.
- API and Worker should call shared layer registration methods so future hosts compose the same Application and Infrastructure setup.
- The first architecture-boundary test checks project references directly from `.csproj` files. This protects the current Clean Architecture direction without adding a heavier architecture-testing package.
- UnitTests now contains architecture guard tests even though pure Domain/Application business unit tests still do not exist yet.
- Keep the first business slice for a later approved milestone.

Do not start authentication, database schema, React frontend, or cloud infrastructure until the relevant milestone is explicitly approved.

Milestone 3 verification:

- Command-line `dotnet test LIAnsureProtect.slnx` passed after restoring packages.
- Command-line `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- Command-line `dotnet test LIAnsureProtect.slnx --no-build` passed; UnitTests passed 5 tests and IntegrationTests passed 3 tests.
- `git diff --check` passed with only normal CRLF warnings on Windows.

### Milestone 4 - Application Use Case Foundation

Status: complete and committed locally as `fe8c27d feat: add application use case foundation`.

Intent:

- Introduce practical CQRS in the Application layer.
- Add MediatR to dispatch commands and queries.
- Add FluentValidation to validate command/query request models before handlers run.
- Add a validation pipeline behavior.
- Add the first small Application business slice: submission intake foundation.
- Add `ISubmissionRepository` as the Application-layer persistence promise for submissions.
- Use Moq for the create-submission handler test because the handler depends on `ISubmissionRepository`.

Created:

- `src/LIAnsureProtect.Domain/Submissions/Submission.cs`
- `src/LIAnsureProtect.Domain/Submissions/SubmissionStatus.cs`
- `src/LIAnsureProtect.Application/Common/Behaviors/ValidationBehavior.cs`
- `src/LIAnsureProtect.Application/Common/Exceptions/ValidationException.cs`
- `src/LIAnsureProtect.Application/Submissions/ISubmissionRepository.cs`
- `src/LIAnsureProtect.Application/Submissions/Commands/CreateSubmission/CreateSubmissionCommand.cs`
- `src/LIAnsureProtect.Application/Submissions/Commands/CreateSubmission/CreateSubmissionCommandHandler.cs`
- `src/LIAnsureProtect.Application/Submissions/Commands/CreateSubmission/CreateSubmissionCommandValidator.cs`
- `src/LIAnsureProtect.Application/Submissions/Commands/CreateSubmission/CreateSubmissionResult.cs`
- `src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs`
- `src/LIAnsureProtect.Infrastructure/Submissions/InMemorySubmissionRepository.cs`
- `tests/LIAnsureProtect.UnitTests/Submissions/SubmissionTests.cs`
- `tests/LIAnsureProtect.UnitTests/Submissions/CreateSubmission/CreateSubmissionCommandHandlerTests.cs`
- `tests/LIAnsureProtect.UnitTests/Submissions/CreateSubmission/CreateSubmissionCommandValidatorTests.cs`
- `tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs`
- `docs/dev/milestone-4-application-use-case-foundation-learnings.md`

Milestone 4 decisions to remember:

- `Submission` uses a private constructor and `CreateDraft(...)` factory method so creation goes through one controlled business door.
- `Submission.Status` has a private setter. Other code can read status, but status changes must go through domain methods such as `Submit()` and `Withdraw()`.
- Use `Withdrawn` for an application that the applicant or broker stops before it becomes a policy. Avoid `Cancelled` here because cancellation often describes ending an active policy.
- `ISubmissionRepository` belongs in Application because handlers need a persistence promise without knowing database details.
- `InMemorySubmissionRepository` is a temporary Infrastructure implementation so the API and Worker can compose before PostgreSQL exists. It is not the future production system of record.
- Moq is appropriate in the handler unit test because the handler depends on an interface.
- Do not add Unit of Work until the persistence milestone introduces EF Core/PostgreSQL and real transaction/save behavior.

Design limits:

- Keep PostgreSQL as one system of record.
- Do not split read and write databases.
- Do not add event sourcing.
- Do not add authentication, database schema, React frontend, or cloud infrastructure unless a later milestone explicitly approves that scope.

Milestone 4 request flow before Milestone 5 persistence work:

```text
POST /api/v1/submissions
  -> SubmissionsController
  -> CreateSubmissionCommand
  -> ValidationBehavior
  -> CreateSubmissionCommandHandler
  -> Submission.CreateDraft(...)
  -> ISubmissionRepository.AddAsync(...)
  -> InMemorySubmissionRepository
```

Milestone 4 verification:

- Command-line `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- Command-line `dotnet test LIAnsureProtect.slnx --no-build` passed; UnitTests passed 14 tests and IntegrationTests passed 5 tests.
- `git diff --check` passed with only normal CRLF warnings on Windows.

## Current Milestone

### Milestone 5 - Persistence Foundation

Status: implemented locally and verified; commit still pending.

Intent:

- Replace the temporary in-memory submission repository with EF Core/PostgreSQL persistence.
- Add a real Infrastructure `DbContext` for submission persistence.
- Add explicit `Submission` mapping so database shape is intentional.
- Add `IUnitOfWork` in Application and EF Core Unit of Work in Infrastructure.
- Update `CreateSubmissionCommandHandler` so the handler stages the submission through the repository and commits through Unit of Work.
- Add Docker Compose for local PostgreSQL with pgvector support.
- Add the first EF Core migration and repo scripts for dependency startup, migration application, and one-command API startup.
- Keep PostgreSQL as the single system of record.

Created:

- `src/LIAnsureProtect.Application/Common/Persistence/IUnitOfWork.cs`
- `src/LIAnsureProtect.Infrastructure/Persistence/SubmissionDbContext.cs`
- `src/LIAnsureProtect.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs`
- `src/LIAnsureProtect.Infrastructure/Persistence/EfCoreUnitOfWork.cs`
- `src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260611012509_CreateSubmissionPersistence.cs`
- `src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260611012509_CreateSubmissionPersistence.Designer.cs`
- `src/LIAnsureProtect.Infrastructure/Persistence/Migrations/SubmissionDbContextModelSnapshot.cs`
- `src/LIAnsureProtect.Infrastructure/Submissions/EfCoreSubmissionRepository.cs`
- `Directory.Packages.props`
- `.config/dotnet-tools.json`
- `.env.example`
- `docker-compose.yml`
- `scripts/common.ps1`
- `scripts/start-dependencies.ps1`
- `scripts/update-database.ps1`
- `scripts/setup-dev.ps1`
- `scripts/dev-up.ps1`
- `scripts/stop-dependencies.ps1`
- `scripts/run-local-ci.ps1`
- `docs/dev/run-the-app.md`
- `docs/dev/dependency-management.md`
- `docs/dev/milestone-5-persistence-foundation-learnings.md`

Updated:

- `src/LIAnsureProtect.Infrastructure/DependencyInjection.cs`
- `src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj`
- `src/LIAnsureProtect.Api/Program.cs`
- `src/LIAnsureProtect.Worker/Program.cs`
- `src/LIAnsureProtect.Api/appsettings.Development.json`
- `src/LIAnsureProtect.Worker/appsettings.Development.json`
- `src/LIAnsureProtect.Application/Submissions/Commands/CreateSubmission/CreateSubmissionCommandHandler.cs`
- `tests/LIAnsureProtect.UnitTests/Submissions/CreateSubmission/CreateSubmissionCommandHandlerTests.cs`
- `tests/LIAnsureProtect.IntegrationTests/DependencyRegistrationTests.cs`
- `tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs`
- `tests/LIAnsureProtect.IntegrationTests/PostgreSqlPersistenceTests.cs`

Removed:

- `src/LIAnsureProtect.Infrastructure/Submissions/InMemorySubmissionRepository.cs`

Milestone 5 decisions to remember:

- The repository stages changes in EF Core; Unit of Work commits them with `SaveChangesAsync`.
- `IUnitOfWork` belongs in Application because handlers need a commit promise without depending on EF Core.
- EF Core types and PostgreSQL provider setup belong in Infrastructure.
- API and Worker hosts pass the `LIAnsureProtect` connection string into Infrastructure.
- The API startup project references `Microsoft.EntityFrameworkCore.Design` as a private design-time package so `dotnet ef database update --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj` can run.
- Repo-local `dotnet-ef`, EF Core design/runtime packages, and SQLite integration-test provider are aligned on `10.0.9` to avoid tool/runtime mismatch warnings and assembly version conflicts.
- NuGet package versions are centralized in `Directory.Packages.props`; project files list package references without repeating versions.
- Repo-local .NET tools are centralized in `.config/dotnet-tools.json`.
- Local service dependencies should run through Docker Compose instead of manual service installs.
- The PostgreSQL container uses a pgvector-enabled image because AI/RAG later expects embeddings in PostgreSQL.
- The PostgreSQL/pgvector image can be overridden with `LIANSUREPROTECT_POSTGRES_IMAGE`; the committed default is `pgvector/pgvector:0.8.2-pg16-trixie`.
- The first migration creates the `submissions` table and enables the PostgreSQL `vector` extension.
- Endpoint integration tests replace PostgreSQL with SQLite in-memory for fast pipeline tests; migration tests verify PostgreSQL migration SQL creates the `vector` extension and `submissions` table.
- The opt-in PostgreSQL-backed integration test verifies the real PostgreSQL/pgvector database has the `vector` extension and persists a `Submission` through EF Core/Npgsql.
- `setup-dev.ps1 -RunTests:$true` enables PostgreSQL-backed tests by setting `LIANSUREPROTECT_RUN_POSTGRES_TESTS=true` and the local test connection string for the duration of the test run.
- `setup-dev.ps1 -RunTests:$true` writes `.trx` test result files under `TestResults/`.
- `run-local-ci.ps1` is the one-command local CI script. It runs fresh setup, PostgreSQL-backed tests, Docker Compose config validation, optional API smoke tests, artifact creation, and cleanup.
- `run-local-ci.ps1` writes each run to `TestResults/local-ci-yyyyMMdd-HHmmss/` and creates `TestResults/local-ci-yyyyMMdd-HHmmss.zip` by default.
- `run-local-ci.ps1` removes the source result folder after the zip artifact is successfully created. If zipping fails, the source folder remains for inspection.
- `run-local-ci.ps1` removes the PostgreSQL container and local database volume by default; pass `-PostgreSqlAfterRun LeaveRunning` to keep PostgreSQL running after verification.
- `setup-dev.ps1` is the non-blocking setup script. By default it stops/removes the involved Compose stack, removes the local PostgreSQL volume, pulls the PostgreSQL/pgvector image when missing, starts dependencies, restores packages, builds, applies committed migrations, and exits. Tests and API startup are opt-in flags.
- `setup-dev.ps1` checks that committed EF Core migration files exist before resetting the local database. Missing migrations fail early with copyable normal console output for the recovery steps: `dotnet tool restore` first, then `dotnet ef migrations add ...`.
- `dev-up.ps1` is the local run wrapper around `setup-dev.ps1 -RunApi:$true`, so it blocks while the API runs.
- `docs/dev/run-the-app.md` is the current step-by-step guide for running the API, PostgreSQL/pgvector dependency, migrations, tests, smoke checks, and troubleshooting.
- Scripts use checked command execution so failed `docker` and `dotnet` commands stop the script and return a failed setup result.
- `update-database.ps1` suppresses EF Core database command logs by default during `dotnet ef database update` to avoid the misleading fresh-database `__EFMigrationsHistory` query failure log; real migration failures still fail through the `dotnet ef` exit code. Use `-SuppressEfCommandLogs:$false` for raw EF command logging.
- Redis should wait until a caching milestone introduces `ICacheService`.
- Kafka should not be added by default. Use transactional outbox with SNS/SQS for planned AWS-native async workflows; consider EventBridge for routing or Amazon MSK only if Kafka-specific requirements appear.
- Do not add authentication, React frontend, cloud infrastructure, domain events/outbox, or event sourcing in this milestone.

Current request flow:

```text
POST /api/v1/submissions
  -> SubmissionsController
  -> CreateSubmissionCommand
  -> ValidationBehavior
  -> CreateSubmissionCommandHandler
  -> Submission.CreateDraft(...)
  -> ISubmissionRepository.AddAsync(...)
  -> EfCoreSubmissionRepository
  -> SubmissionDbContext.Submissions.AddAsync(...)
  -> IUnitOfWork.SaveChangesAsync(...)
  -> EfCoreUnitOfWork
  -> SubmissionDbContext.SaveChangesAsync(...)
```

Milestone 5 verification:

- Current command-line `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors after adding the API startup project EF Core design-time package.
- Current command-line `dotnet test LIAnsureProtect.slnx --no-build --verbosity minimal` passed; UnitTests passed 14 tests and IntegrationTests passed 7 tests with 1 PostgreSQL-backed test skipped by default.
- Full `.\scripts\setup-dev.ps1 -RunTests:$true` passed from an elevated Docker-capable shell; it pulled/started PostgreSQL/pgvector, restored packages, built the solution, restored `dotnet-ef`, applied the committed migration, and ran all tests with UnitTests passing 14 tests and IntegrationTests passing 8 tests with 0 skipped.
- Missing migration guard was verified by temporarily renaming the migrations folder; `setup-dev.ps1` failed before Docker work and printed the expected recovery steps, including `dotnet tool restore`, then the folder was restored.
- Full `.\scripts\run-local-ci.ps1` passed after the missing migration recovery message was updated; it applied the committed migration without the misleading fresh-database `Failed executing DbCommand` log, ran all tests, smoke-tested the API, created `TestResults\local-ci-20260611-093012.zip`, removed the source result folder after zip creation, and cleaned up the PostgreSQL container plus volume.
- `dotnet ef --version` reports `10.0.9` after updating the repo-local tool manifest.
- `docker compose config` rendered a valid Compose configuration; Docker also reported the known local `C:\Users\Poy\.docker\config.json` access warning.
- PowerShell parsing passed for `scripts/common.ps1`, `scripts/start-dependencies.ps1`, `scripts/update-database.ps1`, `scripts/setup-dev.ps1`, `scripts/dev-up.ps1`, `scripts/stop-dependencies.ps1`, and `scripts/run-local-ci.ps1`.
- `git diff --check` passed with only normal CRLF warnings on Windows.

Likely next milestone after closeout:

- To be approved after Milestone 5 closeout. Candidate directions include authentication/authorization foundation, expanding the next submission workflow slice, or introducing the next containerized dependency when the matching feature exists.

## Open Local Setup Items

Known from earlier environment checks:

- .NET 10 is installed.
- Docker is installed, but Docker config access produced a permission warning.
- Node.js/npm is not currently usable from this environment.
- AWS CLI is not currently available from this environment.
- Terraform is not currently available from this environment.

These are not blockers for backend foundation, but Node/npm is required before the React frontend milestone, and AWS CLI/Terraform are required before deployment milestones.
