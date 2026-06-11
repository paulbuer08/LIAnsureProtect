# ADR-005: Application Use Case Patterns

## Status

Accepted

## Context

LIAnsureProtect will grow into multiple business workflows: submissions, insured company profiles, questionnaires, underwriting review, quotes, policies, claims, documents, notifications, audit, and later AI-assisted review.

The project needs an Application-layer pattern that keeps those workflows organized without turning the early modular monolith into premature microservices or a complex distributed system.

Milestone 3 created `AddApplication()` and `AddInfrastructure()` as stable dependency-registration entry points. Milestone 4 is expected to put the first real Application use case behind those entry points.

## Decision

Use practical CQRS in the Application layer.

- Commands represent requests that change state.
- Queries represent requests that read state.
- Command and query handlers live in the Application layer.
- PostgreSQL remains the single system of record at this stage.
- Do not introduce separate read/write databases for initial CQRS.

Use MediatR as the in-process dispatcher for commands and queries.

Use FluentValidation to validate command and query request models before handlers execute.

Use MediatR pipeline behaviors for cross-cutting Application concerns when needed, beginning with validation. Later pipeline behaviors may cover logging, authorization checks, transactions, audit metadata, or domain event dispatching when those features exist.

Use Moq in unit tests when a handler depends on an interface that should be replaced with a test double. Milestone 4 uses Moq for the `CreateSubmissionCommandHandler` test because the handler depends on `ISubmissionRepository`.

Introduce Unit of Work later with persistence, not in the first Application pattern milestone. Unit of Work should coordinate EF Core `DbContext` changes and database transactions when PostgreSQL is introduced.

Do not use event sourcing as the default persistence model.

Domain events and a transactional outbox remain planned later for reliable asynchronous workflows. Event sourcing may be considered later only for selected workflows if replayable history provides enough value to justify the added complexity.

## Consequences

The Application layer gets a consistent shape for business workflows:

```text
API/Worker
  -> MediatR
    -> pipeline behaviors
      -> FluentValidation
      -> command/query handler
        -> Domain rules
        -> Application interfaces
          -> Infrastructure implementations
```

Controllers can stay thin because they translate HTTP requests into Application commands or queries.

Application tests can focus on handlers, validation rules, and business workflow behavior.

The project gains a clear path for future cross-cutting behavior without scattering validation, authorization, transactions, or audit logic across controllers.

The tradeoff is added structure and package dependencies. To avoid empty ceremony, MediatR and FluentValidation should be introduced with the first real Application business slice, not as unused packages.

The first repository implementation may be temporary and in-memory only so the host can compose before database work begins. That temporary implementation is not the real system of record and should be replaced by PostgreSQL-backed Infrastructure in the persistence milestone.

Milestone 5 - Persistence Foundation follows this decision by replacing the temporary in-memory repository with EF Core/PostgreSQL persistence and introducing Unit of Work alongside the EF Core `DbContext` save boundary. It also adds Docker Compose PostgreSQL with pgvector support and the first EF Core migration, keeping PostgreSQL as both the relational system of record and the expected future home for AI/RAG embeddings.

## Not Chosen

Full event sourcing is not chosen now.

Separate read and write databases are not chosen now.

Microservices are not chosen now.

Direct controller-to-service calls without a consistent use-case pattern are not preferred for the long-term Application layer.

Unit of Work in Milestone 4 is not chosen because there is no EF Core `DbContext`, database transaction, or persistence boundary to coordinate yet.
