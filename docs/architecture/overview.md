# Architecture Overview

LIAnsureProtect is designed as a modular, production-style cyber specialty insurance platform.

The core idea is simple:

- PostgreSQL is the system of record, like the official filing cabinet.
- pgvector extends PostgreSQL later for AI/RAG embeddings so vector search stays in the same PostgreSQL system of record.
- Redis is the fast cache, like a sticky note for data we can rebuild.
- DynamoDB is used later for notification read models, like a fast mailbox for each user.
- S3 is used later for private document storage.
- SNS and SQS are used later for durable event processing.

## High-Level Shape

```text
React Frontend
  |
  | HTTPS
  v
ASP.NET Core Web API
  |
  |-- PostgreSQL + pgvector: system of record and later vector search
  |-- Redis: cache
  |-- Local/S3 storage: documents
  |-- Outbox: domain events
  |
Workers
  |
  |-- SQS consumers
  |-- Notification processing
  |-- Audit processing
  |-- AI review processing later
```

## Backend Layers

- Domain: business entities, enums, value objects, and domain rules.
- Application: use cases, DTOs, validators, interfaces, and authorization-friendly business workflows.
- Infrastructure: EF Core, PostgreSQL, Redis, DynamoDB, S3, messaging, and other external services.
- Api: HTTP endpoints, authentication, authorization, middleware, Swagger/OpenAPI, and health checks.
- Workers: background processors and queue consumers.

Application and Infrastructure each expose a dependency-registration extension method. API and Worker startup call those methods so future use cases, repositories, storage services, caches, and messaging adapters can be registered inside their owning layer instead of being scattered through each host.

Current architecture guard tests read the project files and verify the intended project-reference direction:

- Domain references no production project.
- Application references Domain.
- Infrastructure references Application and Domain.
- Api references Application and Infrastructure.
- Worker references Application and Infrastructure.

## Application Use Case Pattern

Milestone 4 - Application Use Case Foundation introduced practical CQRS with MediatR and FluentValidation.

Use practical CQRS inside the modular monolith:

- Commands model Application requests that change state.
- Queries model Application requests that read state.
- Command and query handlers live in the Application layer.
- PostgreSQL remains the single system of record; do not split read and write databases at this stage.

Use MediatR as the in-process dispatcher:

```text
API Controller or Worker
  -> MediatR
  -> pipeline behaviors
  -> command/query handler
```

Use FluentValidation for request validation before handlers run. Validation should check request shape and input rules. Domain objects should still protect business invariants.

Use Moq in unit tests only when a handler depends on an interface that should be replaced with a test double. Do not add Moq unless a test needs it.

The first Milestone 4 submission slice uses this flow:

```text
POST /api/v1/submissions
  -> SubmissionsController
  -> CreateSubmissionCommand
  -> validation pipeline behavior
  -> CreateSubmissionCommandHandler
  -> Submission.CreateDraft(...)
  -> ISubmissionRepository.AddAsync(...)
  -> IUnitOfWork.SaveChangesAsync(...)
```

`ISubmissionRepository` lives in Application because the use case needs a storage promise, not a database detail. `IUnitOfWork` also lives in Application because the use case needs a commit promise without knowing that EF Core performs the actual database save.

Simple analogy:

```text
Application:
  "I need a filing tray for submissions."

Infrastructure today:
  "Here is the real PostgreSQL filing cabinet."
```

Milestone 5 - Persistence Foundation replaced the temporary in-memory repository with EF Core and PostgreSQL persistence in Infrastructure. The current persistence flow is:

```text
CreateSubmissionCommandHandler
  -> ISubmissionRepository.AddAsync(...)
  -> EfCoreSubmissionRepository
  -> SubmissionDbContext.Submissions.AddAsync(...)
  -> IUnitOfWork.SaveChangesAsync(...)
  -> EfCoreUnitOfWork
  -> SubmissionDbContext.SaveChangesAsync(...)
  -> PostgreSQL
```

Local development runs PostgreSQL as a Docker Compose dependency using a pgvector-enabled image. The first persistence migration creates the `vector` extension now so the database is ready for later AI/RAG vector tables without changing the system-of-record decision.

Simple analogy:

```text
Repository:
  "Put this submission into the filing tray."

Unit of Work:
  "Commit everything in the tray to the filing cabinet."
```

The first public business endpoint is:

```text
POST /api/v1/submissions
```

Milestone 6 - Authentication Foundation protects this endpoint with the `Submissions.Create` policy. Anonymous callers receive `401 Unauthorized`, and authenticated callers without an allowed role receive `403 Forbidden`.

Allowed roles for creating submissions:

```text
Customer
Broker
Admin
```

The controller is intentionally thin. It translates HTTP JSON into an Application command and translates Application validation failures into `400 Bad Request` validation problem details. Authentication and authorization run before the Application use case begins.

Recommended Application folder shape once the first business slice exists:

```text
src/LIAnsureProtect.Application/
  Common/
    Behaviors/
      ValidationBehavior.cs
    Exceptions/
      ValidationException.cs

  Submissions/
    Commands/
      CreateSubmission/
        CreateSubmissionCommand.cs
        CreateSubmissionCommandHandler.cs
        CreateSubmissionCommandValidator.cs
    Queries/
      GetSubmissionDetails/
        GetSubmissionDetailsQuery.cs
        GetSubmissionDetailsQueryHandler.cs
```

Recommended request flow:

```text
API/Worker
  -> MediatR
    -> pipeline behaviors
      -> FluentValidation
      -> command/query handler
        -> Domain rules
        -> Application interfaces
          -> Infrastructure implementations
            -> PostgreSQL/storage/cache/messaging later
```

Domain events and a transactional outbox are planned later for reliable asynchronous workflows. Event sourcing is not part of the initial architecture. It may be considered later only for selected workflows if replayable history provides enough value to justify the added complexity.

## Dependency Runtime Direction

Local development should avoid manually installed service dependencies.

Use Docker Compose for application dependencies:

- PostgreSQL with pgvector now.
- Redis later when caching is introduced.
- DynamoDB Local later when notification inbox/read-model work starts.
- LocalStack later when AWS integration workflows need local emulation.
- MailHog or smtp4dev later when email workflows exist.

The app can still run from the local .NET SDK during early development. The important boundary is that external services the app depends on should be containerized and reproducible.

## Messaging Direction

Kafka is not part of the default architecture.

The planned AWS-native messaging path is:

```text
Domain event
  -> transactional outbox
  -> SNS topic
  -> SQS queue
  -> Worker
```

Use SNS when one published event should fan out to one or more subscribers. Use SQS when work needs durable queueing and retry by workers.

Use EventBridge later if the project needs rule-based event routing across AWS services, SaaS integrations, or multiple bounded contexts.

Use Amazon MSK only if a future requirement specifically needs Apache Kafka compatibility, Kafka ecosystem tooling, very high-volume stream processing, or replayable stream consumers.

## API Foundation

The first API baseline uses ASP.NET Core Web API with controllers.

Current foundation:

- OpenAPI document generation for discoverable API contracts.
- ProblemDetails for consistent API error responses.
- Health checks for basic operational status.
- HTTPS redirection for local and production security posture.
- `/api/v1/health` as the first versioned operational endpoint.
- A simple root endpoint that reports the running application name dynamically from the assembly.
- JWT bearer authentication foundation for protected business endpoints.
- Policy-based authorization using Application-owned role and policy names.
- `ICurrentUser` abstraction so Application code can later ask who is making a request without depending on ASP.NET Core HTTP details.

The authentication foundation uses this shape:

```text
External identity provider
  -> signed JWT access token
  -> ASP.NET Core JwtBearer validation
  -> authorization policy
  -> controller
  -> Application use case
```

Simple analogy:

```text
Authentication:
  Read the caller's badge.

Authorization:
  Check whether the badge opens this room.
```

The API validates the configured token issuer, audience, lifetime, signing key, and role claim type. If the required authentication configuration is missing or the authority is not an HTTPS URL, the API fails startup instead of running with unclear security.

Planned API direction:

- Keep public business endpoints under `/api/v1/...` from the beginning.
- Add formal API versioning when the first real business endpoints are introduced or before a breaking API change.
- Generate separate OpenAPI documents later when needed for API versions, public/internal audiences, or frontend/backend API groupings.
- Keep OpenAPI exposed only in development until a later milestone explicitly protects API documentation with role-based access.
- Protect API documentation later with authorization, such as Admin or Developer access, before exposing it outside local development.
- Prefer controller-based APIs for business resources, while allowing small `MapGet` endpoints for infrastructure/status endpoints.
- Treat OpenAPI document caching as a later optimization, and avoid public caching for protected/internal API metadata.

## Deployment Tracks

The project supports two production deployment tracks over time:

- ECS Fargate + ALB for the main containerized web API path.
- Lambda + API Gateway for the serverless API path.

ECS Fargate is the stronger first production path for Docker, autoscaling, ALB, WAF, and zero-downtime blue/green deployment. Lambda/API Gateway is added after the API shape is stable.

## Security Defaults

- HTTPS everywhere outside local development.
- JWT bearer authentication for protected API endpoints.
- Role and policy-based authorization.
- Secrets stored outside source control.
- Private document storage.
- Audit logs for sensitive actions.
- Separate dev, staging, and production environments.

## Production Middleware Direction

Milestone 2 intentionally keeps middleware small. Add production middleware when its supporting feature exists:

- External identity provider tenant integration and login flows when user-facing authentication is introduced.
- CORS when the React frontend runs from a separate origin.
- Forwarded headers when the API runs behind CloudFront, ALB, API Gateway, or another reverse proxy.
- HSTS when HTTPS hosting is finalized outside local development.
- Rate limiting for public API protection.
- Response compression and safe output caching for suitable non-sensitive responses.
- Request correlation, tracing, and enriched structured logging for production observability.
- Readiness and dependency health checks when PostgreSQL, Redis, queues, storage, and external integrations are added.
