# Project Status

This file is the continuity checkpoint for LIAnsureProtect. It should be updated whenever a milestone changes the project direction, architecture, setup, implementation status, or next planned work.

Use this file at the start of a new conversation or coding session before making decisions.

## Current Workspace

- Default project path: `C:\Users\Poy\Documents\LIAnsureProtect`
- Current branch: `codex/milestone-28-evidence-document-security-screening-foundation`
- Git state: Milestone 1 committed locally as `3d16e8c docs: add project foundation`; Milestone 2 committed locally as `f36a8aa feat: add backend foundation`; Milestone 3 committed locally as `bb4b547 feat: add dependency registration and architecture guards`; Milestone 4 planning committed locally as `dab62d0 docs: add application use case foundation plan`; Milestone 4 implementation committed locally as `fe8c27d feat: add application use case foundation`; Milestone 5 implementation committed locally as `2fbdf7f feat: add persistence foundation`; Milestone 5 closeout committed locally as `7cade1a docs: close persistence foundation milestone`; Milestone 6 implementation committed locally as `436ee0e feat: add authentication foundation`; Milestone 7 closeout committed locally as `fcac659 feat: integrate Auth0 identity provider setup`; Milestone 8 implementation committed locally as `2d73027 feat: add frontend login and session foundation`; Milestone 9 implementation committed locally as `689df5b feat: add submission intake UI foundation`; Milestone 10 implementation committed locally as `172fb7b feat: add submission list and detail foundation`; Milestone 10 closeout committed locally as `f68617d docs: close submission list and detail foundation milestone`; Milestone 11 implementation committed locally as `699783d feat: add submission ownership foundation`; Milestone 12 implementation committed locally as `883a243 feat: add submission submit and domain events foundation`; Milestone 13 implementation committed locally as `8f5b65c feat: add transactional outbox foundation`; Milestone 13 closeout committed locally as `4c08d60 docs: close transactional outbox foundation milestone`; Milestone 14 implementation committed locally as `eef3f34 feat: add outbox dispatcher foundation`; Milestone 14 closeout committed locally as `a1f2409 docs: close outbox dispatcher foundation milestone`; Milestone 15 implementation committed locally as `cdc3f86 feat: add idempotent submission actions foundation`; Milestone 15 closeout committed locally as `72c4eca docs: close idempotent submission actions milestone`; Milestone 16 implementation committed locally as `6bba9d0 feat: add idempotency operational cleanup foundation`; Milestone 16 closeout committed locally as `fc22cec docs: close idempotency operational hardening milestone`; Milestone 17 implementation committed locally as `0792023 feat: add cyber rating and quote foundation`; Milestone 17 closeout committed locally as `5753b46 docs: close cyber rating and quote foundation milestone`; Milestone 18 implementation committed locally as `dc8a924 feat: add underwriting referral foundation`; Milestone 18 closeout committed locally as `e18d82d docs: close underwriting referral foundation milestone`; Milestone 19 implementation committed locally as `5106907 feat: add external rating provider resilience foundation`; Milestone 20 implementation committed locally as `ade6297 feat: add quote acceptance and policy binding foundation`; Milestone 20 closeout committed locally as `4d91665 docs: close quote acceptance and policy binding milestone`; Milestone 21 implementation committed locally as `ed0d073 feat: add notification and outbox publishing foundation`; Milestone 21 closeout committed locally as `18c502a docs: close notification and outbox publishing milestone`; Milestone 22 implementation committed locally as `832aa95 feat: add AI underwriting assistant foundation`; Milestone 22 closeout committed locally as `30a576e docs: close AI underwriting assistant milestone`; Milestone 23 implementation committed locally as `cc7735a feat: add underwriting workbench UI foundation`; Milestone 23 closeout committed locally as `68e094a docs: close underwriting workbench UI milestone`; Milestone 24 implementation committed locally as `b57d304 feat: add underwriting referral operations foundation`; Milestone 24 closeout committed locally as `0e169a9 docs: close underwriting referral operations milestone`; Milestone 25 implementation committed locally as `77cad37 feat: add underwriting evidence request foundation`; Milestone 25 closeout committed locally as `de42ded docs: close underwriting evidence request milestone`; Milestone 26 implementation committed locally as `1f790e0 feat: add evidence request notification follow-up foundation`; Milestone 26 closeout committed locally as `5ca941d docs: close evidence request notification follow-up milestone`; Milestone 27 implementation committed locally as `ab2e801 feat: add evidence document storage foundation`; Milestone 27 closeout committed locally as `da28686 docs: close evidence document storage milestone`.
- Latest milestone closeout: Milestone 27 closeout committed locally as `da28686 docs: close evidence document storage milestone`; closeout docs record final local CI artifact `TestResults\local-ci-20260623-080954.zip`.
- Current milestone: Milestone 28 - Evidence Document Security Screening Foundation is implemented locally on branch `codex/milestone-28-evidence-document-security-screening-foundation`. The slice adds a provider-shaped local evidence document scanner, persists scan status and SHA-256 metadata on `quote_evidence_documents`, treats pending/rejected/failed files as not trusted for download, blocks underwriter evidence acceptance until attached documents are clean, and gives owners a replacement upload path that appends clean replacement files while preserving rejected/failed audit rows. Production antivirus/S3/GuardDuty provisioning, EventBridge scan ingestion, asynchronous document workers, OCR, embeddings, RAG, autonomous AI document review, legal hold, retention automation, and a manual malware analyst console remain out of scope. Final local CI passed with artifact `TestResults\local-ci-20260623-160248.zip`.
- Application code status: backend solution and project structure created; API baseline and root/health endpoint integration tests are in place; shared Application and Infrastructure dependency-registration methods have been added; architecture-boundary tests now protect the current project-reference direction; Milestone 4 contains the first submission intake slice using `POST /api/v1/submissions`, MediatR, FluentValidation, a validation pipeline behavior, `ISubmissionRepository`, and Moq-backed handler tests; Milestone 5 replaces temporary in-memory submission storage with EF Core/PostgreSQL persistence, `SubmissionDbContext`, explicit submission mapping, a PostgreSQL-backed repository, Unit of Work, Docker Compose PostgreSQL/pgvector dependency setup, the first EF Core migration, centralized NuGet package versions, and an opt-in PostgreSQL-backed integration test; Milestone 6 adds JWT bearer authentication, policy-based authorization, `ICurrentUser`, role/policy constants, protected submission creation, test-only authentication for integration tests, and local CI smoke coverage for anonymous submission rejection; Milestone 8 has created the first React/Vite frontend under `src/LIAnsureProtect.Web` with Tailwind CSS, React Router, Auth0 React SDK wiring, a local Auth0 SPA config, login/logout flow, callback session display, dashboard session display, and a guarded dashboard route; Milestone 9 adds the first real protected submission intake UI at `/submissions/new` using React Hook Form, Zod, `@hookform/resolvers`, TanStack Query, the current Auth0 access-token flow, co-located frontend tests, and a production-scale feature-owned frontend structure under `src/LIAnsureProtect.Web/src/features/submissions`; Milestone 10 adds protected submission list/detail reads using Application queries, EF Core no-tracking LINQ repository reads, controller read endpoints, protected frontend read routes, and TanStack Query read states; Milestone 11 stores `OwnerUserId` on new submissions, persists `owner_user_id`, scopes list/detail reads to `ICurrentUser.UserId`, and uses a separate `Submissions.Read` policy for protected read endpoints; Milestone 12 adds `POST /api/v1/submissions/{submissionId}/submit`, `Submissions.Submit`, an owned tracked submit load, and in-memory `SubmissionSubmittedDomainEvent` recording on the `Submission` aggregate; Milestone 13 adds PostgreSQL `outbox_messages`, outbox EF mapping, and `SaveChangesAsync` event capture for durable domain-event storage; Milestone 14 adds `IOutboxDispatcher`, local pending-message processing, `OutboxMessage.MarkProcessed(...)`, and Worker polling loop wiring; Milestone 15 adds PostgreSQL-backed `idempotency_records`, `IIdempotencyService`, and `Idempotency-Key` handling for create and submit POST actions; Milestone 16 adds Infrastructure-owned idempotency cleanup, Worker-side hourly cleanup, a seven-day completed-record retention window, and a cleanup-query index; Milestone 17 adds local cyber rating strategies, PostgreSQL `quotes`, protected owner-scoped quote creation for submitted submissions, idempotent quote POST handling, and `QuoteGeneratedDomainEvent` outbox capture; Milestone 18 adds `Quotes.Underwrite`, an underwriter referral queue, approve/decline/adjust review actions, PostgreSQL underwriting audit history, and `QuoteUnderwritingDecisionRecordedDomainEvent` outbox capture; Milestone 19 adds `IRatingProviderClient`, a typed HTTP provider adapter using `Microsoft.Extensions.Http.Resilience`, a simulated provider market indication path, PostgreSQL `quote_rating_provider_attempts`, and safe quote-response provider indication enrichment; Milestone 20 adds `Quotes.Accept`, `Policies.Bind`, PostgreSQL `policies`, PostgreSQL `policy_binding_attempts`, simulated policy binding provider acknowledgement, and `PolicyBoundDomainEvent`; Milestone 21 adds `QuoteAcceptedDomainEvent`, Application-owned notification publishing contracts, an Infrastructure local notification publisher, selected quote/policy outbox-to-notification mapping, and outbox publish retry/failure metadata; Milestone 22 adds `IAiReviewService`, an underwriter-only advisory AI review endpoint for referred quotes, a local simulated AI review provider, PostgreSQL `ai_underwriting_reviews`, prompt/schema/input hash audit fields, structured advisory outputs, and guardrail tests proving AI cannot mutate insurance decision state; Milestone 23 adds a protected React underwriting workbench at `/underwriting/quote-referrals`, frontend underwriting API/hooks/types, risk/expiry triage, advisory AI review display, and manual approve/decline/adjust forms over the existing backend endpoints; Milestone 24 adds PostgreSQL-backed referral operations tables, operations endpoints under the underwriting referrals controller, referral queue operations summaries, timeline reads that combine operational evidence with final review history, and a minimal workbench operations panel for assignment, triage, notes, tasks, and timeline; Milestone 25 adds PostgreSQL-backed `quote_evidence_requests`, underwriter create/accept/cancel evidence endpoints, owner-scoped customer/broker list/respond endpoints, text responses with safe attachment metadata, evidence activity in referral timelines and queue summaries, underwriting workbench evidence controls, and a protected owner evidence response page; Milestone 26 adds evidence request lifecycle/follow-up domain events, local outbox-backed evidence notifications, a manual underwriter follow-up action, referral timeline audit for follow-up reminders, and due/overdue evidence indicators in the workbench and owner evidence page; Milestone 27 adds Application-owned `IDocumentStorageService`, Infrastructure local filesystem evidence storage, PostgreSQL `quote_evidence_documents`, multipart owner evidence uploads with basic file governance, private owner/underwriter document download routes, and React owner/workbench document visibility; Milestone 28 adds Application-owned `IEvidenceDocumentScanner`, Infrastructure local deterministic document screening, scan metadata persistence, fail-closed clean-only download gates, clean-only evidence acceptance, owner replacement uploads for rejected/failed scans, and React scan status/download gating in the owner evidence page and underwriting workbench.

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

At the start of each milestone, create or switch to a milestone-specific branch named from the milestone number and title. Create that branch from the latest committed closeout state of the previous milestone so the new work includes all prior code, docs, tests, scripts, and project-status updates.

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

## Recent Completed Work

### Milestone 5 - Persistence Foundation

Status: complete and committed locally as `2fbdf7f feat: add persistence foundation`.

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

## Recent Completed Work

### Milestone 6 - Authentication Foundation

Status: complete.

Intent:

- Add the first authentication and authorization foundation before expanding deeper user workflows.
- Use standards-based JWT bearer authentication for protected API endpoints.
- Keep the API provider-neutral so Auth0, Amazon Cognito, Microsoft Entra External ID, or another OpenID Connect/OAuth provider can issue tokens later.
- Add Application-owned role and policy names instead of scattering security strings through controllers.
- Add `ICurrentUser` so Application use cases can later ask who is making a request without depending on ASP.NET Core HTTP details.
- Protect the first business endpoint, `POST /api/v1/submissions`, while keeping root and health endpoints anonymous.
- Add integration-test authentication that can prove `401`, `403`, `400`, and `201` behavior without real external tokens.
- Keep implementation narrow. Do not add frontend login, account management, cloud identity automation, payment flows, domain events/outbox, or event sourcing.

Created:

- `src/LIAnsureProtect.Application/Common/Security/ApplicationRoles.cs`
- `src/LIAnsureProtect.Application/Common/Security/ApplicationPolicies.cs`
- `src/LIAnsureProtect.Application/Common/Security/ICurrentUser.cs`
- `src/LIAnsureProtect.Api/Security/AuthorizationPolicies.cs`
- `src/LIAnsureProtect.Api/Security/HttpContextCurrentUser.cs`
- `tests/LIAnsureProtect.IntegrationTests/Security/TestAuthHandler.cs`
- `docs/dev/milestone-6-authentication-foundation-learnings.md`

Updated:

- `Directory.Packages.props`
- `src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj`
- `src/LIAnsureProtect.Api/Program.cs`
- `src/LIAnsureProtect.Api/appsettings.json`
- `src/LIAnsureProtect.Api/appsettings.Development.json`
- `src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs`
- `tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs`
- `scripts/run-local-ci.ps1`
- `docs/dev/run-the-app.md`
- `docs/dev/local-development.md`
- `docs/dev/dependency-management.md`
- `docs/business/user-roles.md`
- `docs/architecture/overview.md`
- `README.md`
- `CHANGELOG.md`
- `docs/project-status.md`

Milestone 6 decisions to remember:

- Use OIDC/OAuth-compatible JWT access tokens for API authentication instead of custom token logic.
- Recommended external provider direction is Auth0 by Okta first, but the API should stay provider-neutral by validating standard JWT claims.
- `Authentication:Authority`, `Authentication:Audience`, and `Authentication:RoleClaimType` are configuration values, not application secrets. Production should supply them through deployment configuration such as environment variables, Parameter Store, or Secrets Manager.
- Startup should fail if `Authentication:Authority` or `Authentication:Audience` is missing, or if the authority is not an HTTPS URL.
- `ICurrentUser` belongs in Application; `HttpContextCurrentUser` belongs in Api.
- Use `GetRoles()` instead of a `Roles` property because gathering role claims performs work.
- Use policies for endpoint permissions because policies age better than hard-coded role checks scattered through controllers.
- `Submissions.Create` currently allows `Customer`, `Broker`, and `Admin`.
- `Underwriter` is intentionally blocked from creating submissions; underwriting review should be a separate workflow.
- Protected endpoint response metadata should use gate order: `401`, `403`, validation/input errors, then success.
- Do not add `401` or `403` to `ApplicationValidationException`; those are security gate outcomes, not validation outcomes.
- Test authentication belongs only in the integration test project and uses explicit `X-Test-*` headers.
- `Task.FromResult(...)` is acceptable in the test authentication handler because the handler performs synchronous header/claim work behind an async framework method.
- Local CI now expects anonymous submission creation to return `401 Unauthorized`; authenticated smoke testing can wait for real dev-token or identity-provider setup.

Current protected submission request flow:

```text
POST /api/v1/submissions
  -> JWT authentication
  -> Submissions.Create authorization policy
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

Milestone 6 verification so far:

- `dotnet build LIAnsureProtect.slnx --no-restore` passed after the JWT/authentication setup.
- `dotnet test LIAnsureProtect.slnx --no-build --filter SubmissionEndpointTests` passed with the protected endpoint tests, including anonymous, forbidden, validation, success, and authorized-role cases.
- `dotnet test LIAnsureProtect.slnx --no-build --filter HealthEndpointTests` passed, confirming root and health endpoints remain anonymous.
- `dotnet test LIAnsureProtect.slnx --no-build` passed with UnitTests passing 14 tests and IntegrationTests passing 12 tests with 1 PostgreSQL-backed test skipped by default.
- PowerShell parsing for `scripts/run-local-ci.ps1` passed after the anonymous `401` smoke-test update.
- `git diff --check` passed with only normal Windows line-ending warnings.
- Full `.\scripts\run-local-ci.ps1` passed from a Docker-capable shell after the authentication smoke-test update; it built the solution with 0 warnings/errors, applied the committed migration, ran UnitTests with 14 passed, ran IntegrationTests with 13 passed and 0 skipped including the PostgreSQL-backed test, validated Docker Compose config, smoke-tested the API, verified anonymous submission creation returns `401 Unauthorized`, created `TestResults\local-ci-20260613-094642.zip`, and cleaned up the PostgreSQL container plus volume.

## Current Milestone

### Milestone 7 - Identity Provider Integration

Status: complete.

Branch:

```text
codex/milestone-7-identity-provider-integration
```

Starting point:

```text
811c893 docs: close authentication foundation milestone
```

Approved scope:

- Connect the JWT bearer foundation to a real Auth0 tenant.
- Document the local developer login/token workflow.
- Represent real Auth0 roles in access tokens through a namespaced custom claim such as `https://liansureprotect.local/roles`.
- Configure the API to use that namespaced claim through `Authentication:RoleClaimType`.
- Start with manual access-token testing before adding automated token helpers or frontend login.
- Keep implementation narrow and provider-neutral at the API boundary where practical.

Current Auth0 setup progress:

- Auth0 tenant was created for development.
- Auth0 API `LIAnsureProtect API` was created with audience `https://api.liansureprotect.local`.
- Auth0 API RBAC was enabled.
- Auth0 API permissions are intentionally not included in access tokens yet because the current API enforces roles through ASP.NET Core policies.
- Auth0 roles were created for `Customer`, `Broker`, `Underwriter`, `ClaimsAdjuster`, and `Admin`.
- A test user was verified and assigned the `Customer` role.
- Auth0 post-login Action `Add LIAnsureProtect Roles Claim` was deployed and attached to the post-login trigger.
- `LIAnsureProtect Dev Token Tester` was authorized for user-delegated access to `LIAnsureProtect API`.
- Local development authentication configuration uses committed project constants for audience `https://api.liansureprotect.local` and role claim type `https://liansureprotect.local/roles`; the real Auth0 tenant authority should be supplied locally through ASP.NET Core User Secrets or environment variables.
- Manual Auth0 authorization-code token exchange succeeded and returned an access token for the verified `Customer` test user.
- Manual authenticated smoke test against `POST /api/v1/submissions` succeeded with the Auth0 access token and returned a draft submission.
- Manual anonymous smoke test against `POST /api/v1/submissions` returned `401 Unauthorized`, confirming the endpoint remains protected without a bearer token.
- Manual authenticated-but-unauthorized smoke test against `POST /api/v1/submissions` returned `403 Forbidden` with an Auth0 `Underwriter` token, confirming disallowed roles remain blocked.
- Local build was reported successful after the Auth0 development configuration, documentation updates, and explanatory `Program.cs` comments.
- Full local CI was reported successful after the manual Auth0 access-token smoke testing, User Secrets cleanup, and final documentation updates.
- `docs/dev/run-the-app.md` now documents the repeatable manual Auth0 access-token smoke-testing workflow, including token exchange, `201`, `401`, and `403` checks.

Current Milestone 7 boundary:

- Do not add React login UI, user registration screens, database user profile tables, refresh-token handling, admin user-management UI, ownership checks, or AWS deployment in Milestone 7 unless explicitly approved later.
- Do not add JWE, DPoP, mTLS, transactional authorization with MFA, or refresh-token/offline-access support in Milestone 7. These are future security-hardening milestones after the basic Auth0 JWT flow is verified.

Closeout:

```text
fcac659 feat: integrate Auth0 identity provider setup
```

Verification:

- Manual Auth0 access-token smoke testing passed for `201`, `401`, and `403` behavior.
- Full local CI passed after User Secrets cleanup and final documentation updates.
- `git diff --check` passed before closeout commit.

Future security milestones to plan after Milestone 7:

- Fine-grained authorization milestone:
  - Evolve from role-only checks to role + permission + ownership checks.
  - Example:
    ```text
    Role: Customer
    Permission: create:submissions
    Ownership rule: only create for own company
    ```
  - Keep broad Auth0 roles such as Customer, Broker, Underwriter, ClaimsAdjuster, and Admin.
  - Add API/application permission strings such as `create:submissions`, `read:submissions`, `manage:users`, and `approve:quotes` only when the API code is ready to enforce them.
  - Add ownership-aware policies so users cannot access or mutate records outside their allowed company, broker assignment, underwriting assignment, or claims assignment.
- Token confidentiality hardening milestone:
  - Evaluate JSON Web Encryption (JWE) for access tokens if access tokens ever need to carry confidential claims.
  - Prefer keeping access tokens small and non-sensitive first.
  - Do not enable JWE until the API has explicit decrypt-then-validate support, key management, rotation, and tests.
- Sender-constrained token milestone:
  - Evaluate DPoP for browser/public-client user flows.
  - Evaluate mTLS for backend-to-backend or partner/service integrations.
  - Use sender-constrained tokens to reduce the damage from stolen or leaked bearer tokens.
  - Do not enable this until the client type, Auth0 support, API validation behavior, and local developer workflow are clear.
- Sensitive action step-up authorization milestone:
  - Evaluate Transactional Authorization with MFA or equivalent step-up MFA for high-risk actions.
  - Candidate actions include binding coverage, approving payments, changing admin roles, releasing sensitive documents, and other high-impact insurance workflows.
- React login and session security milestone:
  - Add refresh-token/offline-access support only when the React login/session flow exists.
  - Use secure storage, refresh token rotation, token revocation handling, logout behavior, and tests.
- Identity lifecycle automation milestone:
  - Evaluate Auth0 pre-user-registration Actions for registration guardrails such as blocking disposable email domains, enforcing invite-only broker/admin onboarding, or adding early risk checks before a user record is created.
  - Evaluate Auth0 post-user-registration Actions for application onboarding work such as creating an internal user profile record, sending a welcome/onboarding notification, writing audit events, or starting broker/customer setup workflows after Auth0 creates the user.
  - Evaluate Auth0 send-phone-message Actions only if LIAnsureProtect needs a custom SMS/MFA provider or custom phone-message delivery. Prefer Auth0's managed/default MFA messaging until the product has a clear reason to own phone-message delivery.
  - Evaluate Auth0 password-reset-post-challenge Actions for security auditing, risk checks, or custom notifications after a password reset challenge is passed.
  - Evaluate Auth0 post-change-password Actions for audit logging, security notifications, session/token revocation decisions, and account-risk workflows after a password is changed.
  - Keep these triggers out of Milestone 7 so the first Auth0 integration stays focused on manual JWT access-token validation and roles.

### Milestone 8 - Frontend Login And Session Foundation

Status: complete.

Branch:

```text
codex/milestone-8-frontend-login-and-session-foundation
```

Starting point:

```text
8718bdb docs: close identity provider integration milestone
```

Approved direction:

- Add the first frontend login/session foundation after the API proved real Auth0 access-token validation.
- Use Auth0 Authorization Code with PKCE for browser login.
- Let the frontend obtain an access token for the LIAnsureProtect API audience.
- Use that access token to call protected API endpoints such as `POST /api/v1/submissions`.
- Keep the API provider-neutral. The frontend can use Auth0 SDKs, but Application code should still depend on project security abstractions and JWT claims, not Auth0-specific types.
- Continue guided manual implementation: explain each snippet in simple English before the user applies it.

Simple analogy:

```text
Milestone 7:
  We proved the API door scanner can read a real Auth0 badge.

Milestone 8:
  We build the frontend reception desk that sends the user to Auth0,
  receives the badge, and uses it when calling the API.
```

Initial scope:

- Inspect the current frontend/project structure before choosing exact files.
- Decide whether the React app already exists or needs to be created.
- Current inspection found no existing React/frontend project or `package.json`; Milestone 8 should create the first frontend project.
- React + TypeScript + Vite frontend scaffold was created under `src/LIAnsureProtect.Web`.
- The scaffold installed npm dependencies and started the Vite development server at `http://localhost:5173/`.
- Tailwind CSS was installed for the frontend using the current Vite plugin package `@tailwindcss/vite`.
- React Router was added with `/`, `/callback`, and `/dashboard` routes.
- Auth0 frontend login was configured using Authorization Code with PKCE through `@auth0/auth0-react`.
- A separate Auth0 Single Page Application client, `LIAnsureProtect Web Dev`, was created for the browser app.
- Local frontend config is stored in `src/LIAnsureProtect.Web/.env.local`, which is intentionally ignored by git and contains only public browser configuration values.
- The frontend can log in through Auth0, display callback/session state, show the signed-in dashboard, log out, and guard `/dashboard` from unauthenticated direct access.
- The dashboard can request an Auth0 access token for the LIAnsureProtect API audience and display a safe shortened preview.
- A development CORS policy now allows the local frontend origin `http://localhost:5173` to call the API during browser-based Milestone 8 testing.
- The frontend now has a small fetch-based API client and a protected dashboard smoke-test button that can create a draft submission through `POST /api/v1/submissions` using the Auth0 access token.
- Focused frontend tests now cover the authenticated route guard and dashboard token/API smoke behavior.
- `run-local-ci.ps1` can now run frontend install/build/lint/test checks when the web project exists.
- The real browser smoke test passed: the signed-in Auth0 user clicked `Create draft submission`, the dashboard displayed submission `826637af-7ca2-4715-bd71-eb9836caeb7c` with `Draft` status, and a direct PostgreSQL container query confirmed the row was persisted in the `submissions` table.
- Final local CI passed with backend build, migrations, backend tests, API smoke tests, frontend build, frontend lint, and frontend tests using `.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false`; artifact zip: `TestResults\local-ci-20260617-021009.zip`.
- `-RunFrontendInstall:$false` was used because this machine still had Windows file locks under `node_modules`; the frontend build, lint, and tests still ran and passed.
- Keep learning notes rich, with simple explanations, examples, analogies, diagrams, and verification commands.

Out of scope unless explicitly approved later:

- User registration screens.
- Admin user-management UI.
- Database user profile table.
- Ownership checks.
- Fine-grained permission enforcement.
- JWE.
- DPoP or mTLS sender-constrained tokens.
- Transactional authorization with MFA.
- Production AWS deployment.

Refresh-token/offline-access direction:

- Do not enable refresh tokens automatically at the start of Milestone 8.
- First understand the basic login, access-token request, and protected API call.
- Add refresh-token/offline-access only if the session behavior needs it and the storage, rotation, logout, and testing strategy are documented.

### Milestone 9 - Submission Intake UI Foundation

Status: implemented. Frontend verification and signed-in Auth0 full-stack browser smoke testing passed.

Branch:

```text
codex/milestone-9-submission-intake-ui-foundation
```

Starting point:

```text
7a99186 docs: close frontend login and session foundation milestone
```

Approved direction:

- Turn the dashboard-only smoke-test button into the first real frontend workflow for creating a submission.
- Add a protected `/submissions/new` route.
- Use a small form for applicant name, applicant email, and company name.
- Validate the form in the browser with friendly inline messages.
- Use the existing Auth0 access-token flow to call `POST /api/v1/submissions`.
- Use TanStack Query to represent the loading, success, and error state of the create-submission API mutation.
- Keep tests co-located with the pages they protect.
- Keep the milestone narrow and do not start questionnaire, document, quote, ownership, admin, deployment, or advanced token-hardening work.

Implemented:

- Added frontend dependencies:
  - `react-hook-form`
  - `zod`
  - `@hookform/resolvers`
  - `@tanstack/react-query`
- Added `QueryClientProvider` at the frontend root.
- Added `/submissions/new` as a protected route.
- Added `NewSubmissionPage` with Zod-backed validation and React Hook Form field wiring.
- Updated the dashboard to link to the real Create submission page instead of keeping the hard-coded protected API smoke-test submit section.
- Exported the `CreateSubmissionRequest` type from the frontend API client.
- Added co-located tests for:
  - form rendering
  - validation errors
  - successful submit with mocked Auth0 token and API client
  - dashboard navigation to the create-submission page
- Refactored the submission intake workflow into a feature-owned vertical slice under `src/LIAnsureProtect.Web/src/features/submissions`:
  - `api/createSubmission.ts`
  - `components/SubmissionIntakeForm.tsx`
  - `hooks/useCreateSubmission.ts`
  - `pages/NewSubmissionPage.tsx`
  - `schemas/submissionIntakeSchema.ts`
  - `types.ts`
- Added `docs/dev/milestone-9-submission-intake-ui-foundation-learnings.md`.

Current verification:

- Focused RED test failed before implementation because `NewSubmissionPage` did not exist and the dashboard did not link to `Create submission`.
- Focused GREEN test passed after implementation: 2 test files, 5 tests passed.
- Full frontend test command passed: 3 test files, 8 tests passed.
- TypeScript build check passed.
- ESLint check passed.
- Vite production build passed without the chunk-size warning after adding route-level code splitting for frontend pages.
- Browser verification against `http://127.0.0.1:5173/submissions/new` passed for the protected unauthenticated route state, with the generic Auth0 login guard visible and no console warnings or errors.
- Full signed-in Auth0 browser submission creation passed: the user created draft submission `3d80c3c8-e96e-4bc6-8fc5-f2c425383b7b` from `/submissions/new`.
- PostgreSQL persistence verification passed in DBeaver and by direct container query: `public.submissions` contains the created `Draft` row for `Jane Applicant`, `jane@example.com`, and `Example Company`.
- Final local CI passed in the user's PowerShell session after the browser and database smoke checks.
- Feature-structure refactor verification passed with focused frontend tests, TypeScript build, and a post-refactor browser route check after the move.

Current Milestone 9 boundary:

- Do not add ownership rules.
- Do not add the multi-step insurance questionnaire.
- Do not add file uploads or document storage.
- Do not add quote generation.
- Do not add admin screens.
- Do not add refresh tokens.
- Do not add deployment.
- Do not add DPoP, mTLS, or JWE.
- Do not expand into a full design system milestone.

### Milestone 10 - Submission List And Detail Foundation

Status: complete. Automated local CI passed, and the real signed-in Auth0 browser smoke passed from the user's browser session. The detailed implementation plan is captured in `docs/superpowers/plans/2026-06-19-milestone-10-submission-list-and-detail-foundation.md`, and the learning notes are captured in `docs/dev/milestone-10-submission-list-and-detail-foundation-learnings.md`.

Branch:

```text
codex/milestone-10-submission-list-and-detail-foundation
```

Starting point:

```text
689df5b feat: add submission intake UI foundation
```

Approved direction:

- Add the first protected read workflow after Milestone 9's create-submission workflow.
- Add `GET /api/v1/submissions` for a submission list.
- Add `GET /api/v1/submissions/{submissionId}` for a submission detail view.
- Continue practical CQRS with MediatR by adding Application query handlers instead of putting read logic in controllers.
- Use REPR-style request/endpoint/response thinking without replacing the current controller-based API shape.
- Extend the existing `ISubmissionRepository` with read promises and implement them through EF Core no-tracking queries.
- Keep PostgreSQL as the system of record; do not add a separate read database.
- Add protected frontend routes for `/submissions` and `/submissions/:submissionId`.
- Keep frontend code in the existing feature-owned submissions vertical slice under `src/LIAnsureProtect.Web/src/features/submissions`.
- Use TanStack Query to represent loading, empty, error, success, and not-found read states.
- Update dashboard navigation so signed-in users can both view submissions and create a new submission.
- Add focused backend unit tests, backend integration tests, frontend route/page tests, and final local CI/browser verification.

Implemented:

- Added `ListSubmissionsQuery`, `ListSubmissionsQueryHandler`, and `ListSubmissionsResult`.
- Added `GetSubmissionDetailQuery`, `GetSubmissionDetailQueryHandler`, and `SubmissionDetailResult`.
- Extended `ISubmissionRepository` with list/detail read promises.
- Implemented EF Core read methods with LINQ:
  - `AsNoTracking()` because the list/detail pages only display data.
  - `OrderByDescending(...)` so newest submissions appear first.
  - `Where(...)` for detail lookup by id.
  - `Select(...)` projections so the query only reads the fields the response needs.
- Did not add `Include(...)` or `AsSplitQuery()` because the current `Submission` aggregate has no navigation graph to eager-load or split.
- Did not add `HasQueryFilter(...)` because ownership filtering is intentionally deferred until the app has internal user/profile ownership data.
- Added protected `GET /api/v1/submissions`.
- Added protected `GET /api/v1/submissions/{submissionId}` with `404 Not Found` for missing submissions.
- Reused the existing `Submissions.Create` policy for the read endpoints as a deliberate Milestone 10 simplification. A future security milestone should add `Submissions.Read` when fine-grained permissions and ownership rules are ready.
- Added frontend API functions, hooks, routes, and pages for `/submissions` and `/submissions/:submissionId`.
- Added dashboard navigation for both "View submissions" and "Create submission".
- Added focused backend unit tests, backend integration tests, and frontend tests for the new read workflow.

Current verification:

- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-build` passed with UnitTests 17 passed and IntegrationTests 17 passed, 1 skipped PostgreSQL opt-in test.
- Frontend TypeScript compile with `tsc -b` passed.
- Frontend ESLint passed.
- Frontend Vitest passed with 5 test files and 16 tests.
- Vite production build passed and emitted separate route chunks for `SubmissionsPage` and `SubmissionDetailPage`.
- Full `.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false` passed using a temporary npm command shim for this Codex shell; artifact zip: `TestResults\local-ci-20260619-172803.zip`.
- The local CI run started PostgreSQL/pgvector, applied the committed migration, ran backend tests, validated Docker Compose config, ran frontend build/lint/tests, produced the artifact zip, and cleaned up the PostgreSQL container, volume, and network.
- Real signed-in Auth0 browser smoke passed on 2026-06-19 from the user's browser session. The user confirmed `/submissions` showed draft submission `e4baab2b-44c0-4619-b961-bc1d4d0a3c70` for `Jane Applicant`, `jane@example.com`, and `Example Company`, and `/submissions/e4baab2b-44c0-4619-b961-bc1d4d0a3c70` showed the expected id, `Draft` status, applicant, email, company, and created UTC timestamp `2026-06-19T09:39:46.944492Z`.

Current Milestone 10 boundary:

- Do not add ownership filtering yet. That should wait until internal user/profile ownership data exists.
- Do not add fine-grained `Submissions.Read` permissions unless the implementation remains very small; otherwise backlog it for a security-hardening milestone.
- Do not add edit, submit-for-review, withdraw, questionnaire, document upload, quote generation, admin screens, deployment, API Gateway, or BFF.
- Do not add domain events, outbox, inbox, saga/process manager, idempotency, cache-aside, external provider adapters, retry, or circuit breaker in this milestone.
- Do not rewrite controllers to Minimal APIs or endpoint-per-class REPR handlers. Use REPR as a design lens only.

Noteworthy learning goals:

- Understand how the read side of CQRS differs from the create command already built in Milestone 9.
- Learn why EF Core `AsNoTracking()` is appropriate for read-only list/detail queries.
- Learn how TanStack Query models server-state reads differently from create mutations.
- Learn how vertical slices can contain backend and frontend feature work without turning pages or controllers into large mixed files.
- Learn how to defer good patterns until the product has a concrete need for them.

### Milestone 11 - Submission Ownership Foundation

Status: complete and committed locally as `699783d feat: add submission ownership foundation`.

Branch:

```text
codex/milestone-11-submission-ownership-foundation
```

Starting point:

```text
f68617d docs: close submission list and detail foundation milestone
```

Implemented direction:

- Keep the milestone focused on ownership and authorization boundaries, not on underwriting workflow expansion.
- Introduced the smallest internal ownership model that connects an authenticated user to submissions they created.
- Added `Submission.OwnerUserId` and persisted it as `owner_user_id`.
- Added EF Core migration `20260619100855_AddSubmissionOwnership` with an owner/date index for scoped list reads.
- Updated `CreateSubmissionCommandHandler` to stamp new submissions with `ICurrentUser.UserId`.
- Updated list/detail Application query handlers to pass `ICurrentUser.UserId` into repository reads.
- Updated EF Core repository read methods to filter by `OwnerUserId`.
- Added `Submissions.Read` so protected read endpoints no longer reuse the create policy name.
- Added tests proving one signed-in user cannot list or open another user's submission.
- Keep `Include(...)`, `AsSplitQuery()`, and lazy loading deferred unless this milestone also adds a real navigation graph that needs them.

Current verification:

- Focused RED unit test run failed before implementation because `Submission.OwnerUserId`, owner-aware `CreateDraft(...)`, owner-scoped repository methods, and owner-aware query handler constructors did not exist yet.
- `dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore` passed with 17 tests.
- Initial integration test run was blocked by a stale local `LIAnsureProtect.Api.exe` process holding API output DLL locks. The process was stopped and the integration test run was repeated.
- `dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore` passed with 19 tests and 1 skipped PostgreSQL opt-in test.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 17 passed and IntegrationTests 19 passed, 1 skipped PostgreSQL opt-in test.
- Full local CI passed with Docker-backed PostgreSQL, both migrations applied, backend tests, Docker Compose config validation, frontend build/lint/tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260619-185327.zip`.
- Earlier pattern recommendations have been revisited and turned into a concrete future roadmap in `docs/dev/pattern-roadmap-after-milestone-11.md`. Key decision: Milestone 10 already implemented REPR as a design lens, not an endpoint-per-class rewrite, and the larger patterns should start after Milestone 11 closes.

Current Milestone 11 boundary:

- Do not add questionnaires, document uploads, underwriting notes, quote generation, or external provider adapters yet.
- Do not add organization/team ownership unless it is needed for the smallest safe ownership model.
- Do not add lazy loading by default. If it is introduced later, it should be an explicit learning comparison, not the default data-access strategy.
- Do not convert the API to an API Gateway or BFF shape in this milestone.

### Milestone 12 - Submission Submit And Domain Events Foundation

Status: complete and committed locally as `883a243 feat: add submission submit and domain events foundation`.

Branch:

```text
codex/milestone-12-submission-submit-and-domain-events-foundation
```

Starting point:

```text
699783d feat: add submission ownership foundation
```

Implemented direction:

- Add a real submit action for owned draft submissions.
- Keep the ownership boundary from Milestone 11: users can submit only their own draft submissions.
- Use the existing `Submission.Submit()` domain method as the core business transition.
- Introduce the first domain event around a meaningful business fact, preferably `SubmissionSubmittedDomainEvent`.
- Keep event recording in-memory on the aggregate in this milestone; do not add the transactional outbox yet.
- Add protected API endpoint `POST /api/v1/submissions/{submissionId}/submit`.
- Add Application command/handler/result for the submit action.
- Add repository support to load an owned submission for update.
- Add focused tests proving owner submit succeeds, cross-owner submit does not leak or mutate data, repeated submit is rejected, and the submit event is raised.

Implemented:

- Added `IDomainEvent` as the first Domain-layer event abstraction.
- Added `SubmissionSubmittedDomainEvent` with submission id, owner user id, and occurrence timestamp.
- Added aggregate-owned `DomainEvents` and `ClearDomainEvents()` on `Submission`.
- Updated `Submission.Submit()` so the same domain method changes status and records the submitted event.
- Added `SubmitSubmissionCommand`, `SubmitSubmissionCommandHandler`, and `SubmitSubmissionResult`.
- Added owner-scoped tracked repository load through `GetOwnedForUpdateAsync(...)`.
- Added `Submissions.Submit` authorization policy for Customer, Broker, and Admin roles.
- Added controller action `POST /api/v1/submissions/{submissionId}/submit`.
- Returned `404 Not Found` for missing or cross-owner submissions so record existence is not leaked.
- Returned `409 Conflict` when a non-draft submission is submitted again.
- Updated EF Core configuration to ignore in-memory domain events until a later outbox milestone persists event messages.

Current verification:

- Focused RED unit test run failed before implementation because `SubmitSubmission` command types did not exist.
- `dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore` passed with 22 tests.
- `dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore` passed with 24 tests and 1 skipped PostgreSQL opt-in test.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 22 passed and IntegrationTests 24 passed, 1 skipped PostgreSQL opt-in test.
- Full local CI passed with Docker-backed PostgreSQL, migrations, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260619-193252.zip`.

Current Milestone 12 boundary:

- Do not add outbox storage yet. That is planned for Milestone 13.
- Do not add notifications, worker dispatch, SNS/SQS, email, retry/circuit breaker, idempotency, quote generation, or underwriting queues.
- Do not add organization/team ownership or admin bypass unless explicitly approved later.
- Do not start questionnaire, document upload, or premium calculation in this milestone.

### Milestone 13 - Transactional Outbox Foundation

Status: complete and committed locally as `8f5b65c feat: add transactional outbox foundation`.

Branch:

```text
codex/milestone-13-transactional-outbox-foundation
```

Starting point:

```text
b7e62c8 docs: close submission submit and domain events milestone
```

Implemented direction:

- Add a PostgreSQL `outbox_messages` table through EF Core migration.
- Keep the outbox in the same PostgreSQL database as `submissions`.
- Do not use a separate database or NoSQL store for the write-side outbox.
- Capture aggregate domain events during `SubmissionDbContext.SaveChangesAsync(...)`.
- Persist serialized domain events in the same database save boundary as the submission status update.
- Keep actual publishing and Worker dispatch deferred.

Implemented:

- Added Infrastructure-owned `OutboxMessage`.
- Added `OutboxMessageConfiguration`.
- Added `SubmissionDbContext.OutboxMessages`.
- Updated `SubmissionDbContext.SaveChangesAsync(...)` to convert `Submission.DomainEvents` into outbox rows before saving and clear events after a successful save.
- Added EF Core migration `20260620012622_AddTransactionalOutbox`.
- Added integration test coverage proving `POST /api/v1/submissions/{submissionId}/submit` writes a `SubmissionSubmittedDomainEvent` outbox row.
- Added migration script guard coverage proving committed migrations create `outbox_messages` and the pending-message index.

Current verification:

- Focused RED integration test run failed before implementation because `LIAnsureProtect.Infrastructure.Persistence.Outbox` did not exist.
- Second RED integration test run failed because committed migrations did not yet create `outbox_messages`.
- `dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore` passed with 25 tests and 1 skipped PostgreSQL opt-in test.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 22 passed and IntegrationTests 25 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, migrations, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260620-181324.zip`.

Current Milestone 13 boundary:

- Do not add SNS/SQS.
- Do not add Worker dispatch yet.
- Do not add email notification.
- Do not add retry/circuit breaker.
- Do not add idempotency keys.
- Do not add notification inbox/read model.
- Do not add quote generation or underwriting queues.

### Milestone 14 - Outbox Dispatcher Foundation

Status: complete and committed locally as `eef3f34 feat: add outbox dispatcher foundation`.

Branch:

```text
codex/milestone-14-outbox-dispatcher-foundation
```

Starting point:

```text
4c08d60 docs: close transactional outbox foundation milestone
```

Implemented direction:

- Keep the milestone focused on a local Worker-side dispatcher foundation.
- Read pending `outbox_messages` rows where `processed_at_utc` is null.
- Mark processed rows by setting `processed_at_utc`.
- Keep dispatch local and in-process for learning.
- Do not add SNS/SQS, email, notification inbox/read model, full retry policy, circuit breaker, idempotency keys, quote generation, or underwriting queues.

Implemented:

- Added Infrastructure-owned `IOutboxDispatcher`.
- Added `OutboxDispatcher` that reads oldest pending messages in a small batch and marks them processed.
- Added `OutboxMessage.MarkProcessed(...)`.
- Registered `IOutboxDispatcher` through `AddInfrastructure(...)`.
- Replaced the Worker template loop with a scoped polling loop that resolves `IOutboxDispatcher` and runs `DispatchPendingMessagesAsync(...)`.
- Added integration test coverage proving the dispatcher marks a pending outbox message processed.
- Added dependency-registration coverage proving Infrastructure provides `IOutboxDispatcher`.
- Added `docs/dev/milestone-14-outbox-dispatcher-foundation-learnings.md`.

Current verification:

- Focused RED integration test run failed before implementation because `IOutboxDispatcher` and `OutboxDispatcher` did not exist.
- First GREEN attempt found a dependency-registration issue because the dispatcher required `ILogger<OutboxDispatcher>` in a bare Infrastructure service collection.
- The dispatcher logging dependency was removed because it was not essential to the milestone; the Worker still logs processed message counts.
- `dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore` passed with 26 tests and 1 skipped PostgreSQL opt-in test after the fix.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 22 passed and IntegrationTests 26 passed, 1 skipped PostgreSQL opt-in test.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-004342.zip`.

Current Milestone 14 boundary:

- Do not add SNS/SQS.
- Do not add real email/SMS notification.
- Do not add notification inbox/read model.
- Do not add full retry policy.
- Do not add circuit breaker.
- Do not add idempotency keys.
- Do not add quote generation or underwriting queues.

Next recommended milestone:

```text
Milestone 15 - Idempotent Submission Actions Foundation
```

Recommended starting point:

```text
a1f2409 docs: close outbox dispatcher foundation milestone
```

### Milestone 15 - Idempotent Submission Actions Foundation

Status: complete and committed locally as `cdc3f86 feat: add idempotent submission actions foundation`.

Branch:

```text
codex/milestone-15-idempotent-submission-actions-foundation
```

Starting point:

```text
a1f2409 docs: close outbox dispatcher foundation milestone
```

Implemented direction:

- Protect the current applicable protected POST actions from duplicate client retries.
- Add optional `Idempotency-Key` request-header handling for `POST /api/v1/submissions`.
- Add optional `Idempotency-Key` request-header handling for `POST /api/v1/submissions/{submissionId}/submit`.
- Persist idempotency records in PostgreSQL with key, owner user id, action name, request fingerprint, status, stored response body/status/content type/location, and timestamps.
- Use a unique database index on the idempotency key to create a hard duplicate-key guard.
- Return the stored response for matching safe retries.
- Reject unsafe key reuse with `409 Conflict` when the key is reused for a different owner, action, request body, or submission id.
- Prove repeated matching create requests do not create duplicate draft submissions.
- Prove repeated matching submit requests do not create duplicate downstream effects, especially duplicate outbox messages.
- Document the full flow in `docs/dev/milestone-15-idempotent-submission-actions-foundation-learnings.md`.

Current verification:

- RED integration test run failed before implementation because duplicate create produced a second submission, duplicate submit returned conflict instead of replay, unsafe key reuse was accepted, and the committed migration script did not create `idempotency_records`.
- `dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore` passed with 31 tests and 1 skipped PostgreSQL opt-in test after implementation.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore` passed with 22 tests.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 22 passed and IntegrationTests 31 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260620181233_AddIdempotencyRecords`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-022226.zip`.

Current Milestone 15 boundary:

- Distributed cache.
- Payment-style idempotency complexity.
- Expiration/cleanup jobs for old idempotency records.
- Replay/conflict/in-progress metrics and tracing.
- Making `Idempotency-Key` mandatory for all high-risk POST endpoints.
- SNS/SQS, email, notification inboxes, quote generation, and underwriting queues.
- Any change that bypasses the existing submission ownership boundary.

Future idempotency rule:

- Every future important protected POST endpoint should be reviewed for idempotency. If retrying the endpoint can create duplicate state or duplicate side effects, it should use the Milestone 15 pattern.

Next recommended milestone:

```text
Milestone 16 - Idempotency Operational Hardening Foundation
```

Recommended direction:

- Keep the focus on hardening the idempotency foundation before returning to premium calculation strategy work.
- Add cleanup/expiry behavior for old completed idempotency records.
- Add explicit in-progress recovery behavior for abandoned records.
- Add observability around completed, replayed, conflicted, and in-progress idempotency requests.
- Decide whether selected high-risk POST endpoints should require `Idempotency-Key`.
- Document the convention future write endpoints should follow when they opt into idempotency.

### Milestone 16 - Idempotency Operational Hardening Foundation

Status: complete and committed locally as `6bba9d0 feat: add idempotency operational cleanup foundation`.

Branch:

```text
codex/milestone-16-idempotency-operational-hardening-foundation
```

Starting point:

```text
72c4eca docs: close idempotent submission actions milestone
```

Implemented direction:

- Harden the PostgreSQL-backed idempotency foundation introduced in Milestone 15.
- Add cleanup/expiry behavior for old completed idempotency records.
- Add `IIdempotencyRecordCleanup` and `EfCoreIdempotencyRecordCleanup` in Infrastructure.
- Delete only `Completed` idempotency records whose `completed_at_utc` is older than the retention cutoff.
- Keep `InProgress` idempotency records during cleanup so abandoned-record recovery remains an explicit future behavior.
- Add Worker-side cleanup about once per hour with a seven-day completed-record retention window.
- Add EF Core migration `20260620185535_AddIdempotencyRecordCleanupIndex` for the cleanup-query index.
- Keep the implementation focused on operational hardening, then return to the roadmap's premium calculation strategy milestone unless the project direction changes again.

Deferred hardening:

- Add explicit in-progress recovery behavior for abandoned records.
- Add broader observability around completed, replayed, conflicted, and in-progress idempotency requests.
- Decide whether selected high-risk POST endpoints should require `Idempotency-Key`.
- Document the convention future write endpoints should follow when they opt into idempotency.

Recommended out of scope:

- Premium calculation strategy.
- Quote generation.
- SNS/SQS, email, notification inboxes, and underwriting queues.
- Distributed cache.
- Full payment-provider idempotency semantics.
- Any change that bypasses the existing submission ownership boundary.

Current verification:

- Focused RED integration test run failed before implementation because `IIdempotencyRecordCleanup`, `EfCoreIdempotencyRecordCleanup`, and the cleanup migration index did not exist yet.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- Focused idempotency cleanup and dependency-registration integration tests passed with 4 tests.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 22 passed and IntegrationTests 32 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260620185535_AddIdempotencyRecordCleanupIndex`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-030128.zip`.

Next recommended milestone:

```text
Milestone 17 - Cyber Rating And Quote Foundation
```

### Milestone 17 - Cyber Rating And Quote Foundation

Status: complete and committed locally as `0792023 feat: add cyber rating and quote foundation`.

Branch:

```text
codex/milestone-17-cyber-rating-and-quote-foundation
```

Starting point:

```text
fc22cec docs: close idempotency operational hardening milestone
```

Implemented direction:

- Replace the earlier toy premium-calculation idea with a realistic synthetic cyber rating and quote foundation.
- Add Application-owned cyber rating inputs for industry class, revenue band, requested limit, retention, MFA, EDR, backup maturity, incident response planning, prior cyber incidents, and sensitive data exposure.
- Add baseline and high-risk cyber rating strategies.
- Calculate premium through base premium, industry factor, limit factor, retention credit/debit, control credits/debits, prior incident surcharge, sensitive-data factor, minimum premium, risk tier, subjectivities, and referral reasons.
- Persist generated quotes in PostgreSQL through the `quotes` table.
- Add `POST /api/v1/submissions/{submissionId}/quotes` for owned submitted submissions.
- Keep draft submissions unquotable with `409 Conflict`.
- Keep cross-owner submissions hidden with `404 Not Found`.
- Add `QuoteGeneratedDomainEvent` so generated quotes enter the existing outbox in the same database save boundary.
- Add `Idempotency-Key` support for quote creation so safe retries replay the same quote response without creating duplicate quotes or duplicate outbox messages.

Deferred realistic insurance workflow:

- Milestone 18 should add underwriter referral approval/decline/adjustment.
- Milestone 19 should add an external rating provider adapter with retry and circuit breaker behavior.
- Milestone 20 should add quote acceptance and policy binding.
- Milestone 21 should put notifications and real outbox publishing to use.
- Milestone 22 should add advisory-only AI underwriting assistance with governance and human oversight.

Current verification:

- Focused quote unit tests passed with 2 tests.
- Focused quote endpoint integration tests passed with 5 tests.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 24 passed and IntegrationTests 37 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260621024858_AddCyberQuotes`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-105342.zip`.

Next recommended milestone:

```text
Milestone 18 - Underwriting Referral Foundation
```

### Milestone 18 - Underwriting Referral Foundation

Status: complete with implementation committed locally as `dc8a924 feat: add underwriting referral foundation` and closeout committed locally as `e18d82d docs: close underwriting referral foundation milestone`.

Starting point:

```text
5753b46 docs: close cyber rating and quote foundation milestone
```

Implemented direction:

- Add underwriter-only review actions for quotes that Milestone 17 marks as `Referred`.
- Add a `Quotes.Underwrite` policy for Underwriter and Admin roles.
- Allow underwriters to approve, decline, or adjust a referred quote with audit-friendly reason fields.
- Keep customer and broker ownership separate from underwriter review authority.
- Keep customer/broker users from approving their own referred quotes.
- Persist review decisions in PostgreSQL.
- Add focused backend tests proving authorization, state transitions, audit reason capture, and owner-scope behavior.

Implemented:

- Added `GET /api/v1/underwriting/quote-referrals` for pending referred quote review queue reads.
- Added `POST /api/v1/underwriting/quote-referrals/{quoteId}/approve`.
- Added `POST /api/v1/underwriting/quote-referrals/{quoteId}/decline`.
- Added `POST /api/v1/underwriting/quote-referrals/{quoteId}/adjust`.
- Added current review snapshot fields on `quotes`.
- Added PostgreSQL `quote_underwriting_reviews` audit history.
- Added `QuoteUnderwritingDecisionRecordedDomainEvent` so review decisions enter the existing transactional outbox.
- Added unit and integration tests for review transitions, role authorization, queue reads, persistence, audit rows, outbox capture, and migration shape.

Current verification:

- Focused quote underwriting unit tests passed with 4 tests.
- Focused underwriting referral integration tests and migration guard passed with 7 tests.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 28 passed and IntegrationTests 43 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260621043444_AddQuoteUnderwritingReviews`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-124044.zip`.

Next recommended milestone:

```text
Milestone 19 - External Rating Provider Adapter And Resilience Foundation
```

Milestone 18 deferred scope:

- External rating provider calls.
- Retry and circuit breaker around external HTTP calls.
- Quote acceptance.
- Policy binding and issuing.
- SNS/SQS notification publishing.
- Notification inboxes.
- Advisory AI underwriting assistance.

### Milestone 19 - External Rating Provider Adapter And Resilience Foundation

Status: complete on branch `codex/milestone-19-external-rating-provider-adapter-and-resilience-foundation`.

Starting point:

```text
e18d82d docs: close underwriting referral foundation milestone
```

Implemented direction:

- Added an Application-owned `IRatingProviderClient` boundary for provider-shaped market indication calls.
- Added an Infrastructure typed `HttpClient` adapter using `IHttpClientFactory`.
- Added `Microsoft.Extensions.Http.Resilience` through centralized package management and configured retry, timeout, and circuit-breaker behavior around the outbound provider HTTP call.
- Added a local simulated provider HTTP handler rather than real insurer credentials.
- Added PostgreSQL `quote_rating_provider_attempts` audit persistence for provider status, market disposition, response references, indicated terms, sanitized failure details, attempt count, duration, and request payload hash.
- Enriched quote creation responses with a safe provider indication summary.
- Preserved local quote creation, local premium/status/referral behavior, idempotent quote replay, and underwriting referral behavior when the provider is unavailable or circuit-open.

Current verification:

- Focused provider-boundary unit tests passed with 2 tests.
- Focused quote/provider integration tests, dependency registration, migration guard, retry recovery, and circuit-open tests passed with 10 tests.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 30 passed and IntegrationTests 45 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- `dotnet list LIAnsureProtect.slnx package --vulnerable --include-transitive` reported no vulnerable packages after the integration-test SQLite native bundle override.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260621084502_AddQuoteRatingProviderAttempts`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-170602.zip`.

Closeout:

```text
5106907 feat: add external rating provider resilience foundation
```

Recommended out of scope:

- Real insurer credentials.
- Production provider onboarding.
- Quote acceptance.
- Policy binding and issuing.
- SNS/SQS notification publishing.
- Notification inboxes.
- Advisory AI underwriting assistance.

Next recommended milestone:

```text
Milestone 20 - Quote Acceptance And Policy Binding Foundation
```

### Milestone 20 - Quote Acceptance And Policy Binding Foundation

Status: complete on branch `codex/milestone-20-quote-acceptance-and-policy-binding-foundation`.

Starting point:

```text
811f459 docs: close external rating provider resilience milestone
```

Implementation commit:

```text
ade6297 feat: add quote acceptance and policy binding foundation
```

Implemented direction:

- Added `POST /api/v1/quotes/{quoteId}/accept` for customer, broker, and admin quote acceptance attestations.
- Added `POST /api/v1/quotes/{quoteId}/bind` for creating durable bound policies from accepted local quotes.
- Added quote acceptance audit fields on `quotes`: accepted-by user id, accepted-by name, accepted-by title, subjectivity acknowledgement, and accepted-at timestamp.
- Added PostgreSQL `policies` with policy number, quote id, submission id, owner user id, premium, limit, retention, effective date, expiration date, status, bound audit fields, and quote term snapshot.
- Added PostgreSQL `policy_binding_attempts` for simulated carrier/binding acknowledgement audit evidence.
- Added Application-owned `IPolicyBindingProviderClient` with an Infrastructure simulated binding provider.
- Preserved underwriting authority: `Referred` quotes must be approved or adjusted before acceptance and binding.
- Preserved local quote authority: external provider indications do not bind coverage.
- Added idempotency support for quote acceptance and policy binding.
- Added `PolicyBoundDomainEvent` transactional outbox capture when a policy binds.

Current verification:

- Focused policy binding unit tests passed with 7 tests.
- Focused quote acceptance/policy binding integration tests and migration guard passed with 15 tests.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 37 passed and IntegrationTests 57 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260621125704_AddQuoteAcceptanceAndPolicyBinding`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-210031.zip`.

Closeout result:

- Milestone 20 is complete.
- The final implementation commit is `ade6297 feat: add quote acceptance and policy binding foundation`.
- The final local CI artifact is `TestResults\local-ci-20260621-210031.zip`.
- The recommended next milestone is `Milestone 21 - Notification And Outbox Publishing Foundation`.

Recommended out of scope unless explicitly expanded:

- Real payment collection.
- Production policy document generation.
- Real insurer/carrier binding APIs.
- Real broker/customer e-signature.
- Endorsements, cancellations, renewals, reinstatements, claims, billing, and collections.
- SNS/SQS notification publishing.
- Notification inboxes.
- Advisory AI underwriting assistance.

Next recommended milestone:

```text
Milestone 21 - Notification And Outbox Publishing Foundation
```

### Milestone 21 - Notification And Outbox Publishing Foundation

Status: implemented on branch `codex/milestone-21-notification-and-outbox-publishing-foundation`.

Starting point:

```text
4d91665 docs: close quote acceptance and policy binding milestone
```

Implemented direction:

- Added an Application-owned notification publisher boundary that can publish messages derived from durable outbox rows.
- Added provider-shaped notification message contracts with stable message ids based on the outbox message id.
- Added an Infrastructure local provider-shaped publisher so tests can prove publishing behavior without AWS credentials.
- Added `QuoteAcceptedDomainEvent` so quote acceptance is visible to the outbox and downstream binding/operations notification flow.
- Updated Worker dispatch behavior so selected outbox rows are published through the notification boundary before being marked processed.
- Added publish metadata to `outbox_messages`: attempt count, last attempt time, next retry time, provider message id, and poison failure time.
- Added focused message types for quote ready, quote referred for underwriting, quote underwriting decision recorded, quote accepted, and policy bound.
- Preserved idempotency and retry safety: one business action still writes one outbox row, and downstream messages use the outbox message id as the stable provider message key.
- Added unit and integration tests proving quote acceptance event capture, publish success, transient failure retry, poison/failure recording, dependency registration, and migration shape.

Recommended out of scope unless explicitly expanded:

- Production SNS/SQS publishing.
- Real email/SMS delivery.
- Notification inboxes and read/unread state.
- Complex notification templates.
- User notification preferences.
- Webhooks to external broker/customer systems.

Starting verification path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

Current verification:

- Focused policy binding unit tests passed with 7 tests.
- Focused outbox dispatcher integration tests passed with 4 tests.
- Focused dependency-registration and migration guard tests passed with 3 tests.
- Focused quote acceptance/policy binding endpoint tests passed with 12 tests.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 37 passed and IntegrationTests 60 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260621133523_AddOutboxNotificationPublishingMetadata`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260621-214045.zip`.

Closeout result:

- Milestone 21 is complete.
- The final implementation commit is `ed0d073 feat: add notification and outbox publishing foundation`.
- The final local CI artifact is `TestResults\local-ci-20260621-214045.zip`.
- The recommended next milestone is `Milestone 22 - AI Underwriting Assistant Foundation`.

Recommended out of scope unless explicitly expanded:

- Production SNS/SQS publishing.
- Real email/SMS delivery.
- Notification inboxes and read/unread state.
- User notification preferences.
- External broker/customer webhooks.
- Autonomous AI underwriting decisions.

Next recommended milestone:

```text
Milestone 22 - AI Underwriting Assistant Foundation
```

### Milestone 22 - AI Underwriting Assistant Foundation

Status: complete on branch `codex/milestone-22-ai-underwriting-assistant-foundation`.

Starting point:

```text
18c502a docs: close notification and outbox publishing milestone
```

Implemented direction:

- Added Application-owned `IAiReviewService` and provider-shaped request/result DTOs for advisory underwriting review.
- Added `POST /api/v1/underwriting/quote-referrals/{quoteId}/ai-review`, protected by the existing `Quotes.Underwrite` policy.
- Built AI review context only from existing referred quote fields: premium, limit, retention, risk tier, status, strategy name, subjectivities, referral reasons, owner/submission ids, and prior underwriting review notes when present.
- Added Infrastructure `LocalSimulatedAiReviewService` so local tests need no real model credentials.
- Added PostgreSQL `ai_underwriting_reviews` with prompt version, output schema version, input snapshot hash, structured advisory output JSON, citations, limitations, status, failure reason, optional feedback, requester, and timestamps.
- Kept AI output separate from quote status, policy status, premium, retention, subjectivities, underwriting decision fields, acceptance, and binding state.
- Added focused tests for authorization, advisory persistence, quote immutability, manual underwriting after AI failure, provider output shape, dependency registration, and migration shape.

Recommended out of scope unless explicitly expanded:

- Real production model credentials.
- Autonomous underwriting decisions.
- Replacing the cyber rating engine.
- Changing premium, retention, subjectivities, quote status, or policy status from AI output.
- RAG, uploaded document ingestion, embeddings, prompt-management UI, and customer-facing AI chat.

Current verification:

- Focused AI command handler unit test passed with 1 test.
- Focused AI endpoint, local provider, dependency-registration, and migration guard tests passed with 7 tests.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 38 passed and IntegrationTests 64 passed, 1 skipped PostgreSQL opt-in test.
- `dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build` reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260622002934_AddAiUnderwritingReviews`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup. Artifact zip: `TestResults\local-ci-20260622-104016.zip`.

Closeout result:

- Milestone 22 is complete.
- The final implementation commit is `832aa95 feat: add AI underwriting assistant foundation`.
- The final local CI artifact is `TestResults\local-ci-20260622-104016.zip`.
- The recommended next milestone is `Milestone 23 - Underwriting Workbench UI Foundation`.

### Milestone 23 - Underwriting Workbench UI Foundation

Status: complete on branch `codex/milestone-23-underwriting-workbench-ui-foundation`.

Starting point:

```text
3010719 docs: start underwriting workbench UI milestone
```

Implemented direction:

- Added a protected `/underwriting/quote-referrals` route to the React frontend.
- Added a feature-owned `src/LIAnsureProtect.Web/src/features/underwriting` slice with typed API calls and TanStack Query hooks.
- Built a queue-style underwriter workbench for referred quotes with risk tier, expiry urgency, premium, limit, retention, referral reasons, subjectivities, and client-side triage filters.
- Added advisory AI review request/display UI with executive summary, risk signals, control gaps, questions, subjectivity candidates, citations, limitations, prompt/schema/hash metadata, and advisory-only wording.
- Added manual approve, decline, and adjust forms over the existing backend endpoints.
- Kept backend underwriting authority unchanged and kept AI output separate from manual decision submissions.

Verification:

- Focused frontend tests passed: 3 files, 10 tests.
- Full frontend Vitest passed: 7 files, 24 tests.
- Frontend TypeScript, ESLint, and production build passed.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed.
- EF Core pending model check reported no pending model changes.
- Full local CI passed with artifact `TestResults\local-ci-20260622-120530.zip`.

Closeout result:

- Milestone 23 is complete.
- The final implementation commit is `cc7735a feat: add underwriting workbench UI foundation`.
- The final closeout commit is `68e094a docs: close underwriting workbench UI milestone`.
- The recommended next milestone is `Milestone 24 - Underwriting Referral Operations Foundation`.

### Milestone 24 - Underwriting Referral Operations Foundation

Status: started on branch `codex/milestone-24-underwriting-referral-operations-foundation`.

Starting point:

```text
68e094a docs: close underwriting workbench UI milestone
```

Recommended planning target:

- Add backend-owned operational workflow state for referred quotes.
- Consider underwriter assignment, priority, due date/SLA, persisted work notes, and audit timeline entries.
- Expose only the minimum API and read-model changes needed for the Milestone 23 workbench to show and update those operations safely.
- Keep advisory AI separate from authority-bearing approve, decline, adjust, accept, bind, and issue decisions.

Recommended out of scope unless explicitly expanded:

- Document upload and review.
- Embeddings, RAG, and production AI credentials.
- Autonomous AI approve/decline/adjust decisions.
- Full analytics dashboards.
- Notification inboxes.

## Open Local Setup Items

Known from earlier environment checks:

- .NET 10 is installed.
- Docker is installed, but Docker config access produced a permission warning.
- Node.js/npm is not currently on the Codex shell PATH in this environment; Milestone 9 verification used the bundled Codex Node runtime directly, while the normal project runbook still assumes npm is available on a developer machine.
- AWS CLI is not currently available from this environment.
- Terraform is not currently available from this environment.

These are not blockers for backend foundation, but Node/npm is required before the React frontend milestone, and AWS CLI/Terraform are required before deployment milestones.
