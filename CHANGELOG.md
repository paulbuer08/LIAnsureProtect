# Changelog

All notable changes to LIAnsureProtect will be documented in this file.

The format follows simple milestone-based entries.

## Unreleased

### Added

- Repository foundation files.
- Project status document for continuity across milestone conversations.
- Initial architecture, business, role, local development, and AWS environment documentation.
- Initial architecture decision records for Clean Architecture, React, and PostgreSQL.
- ASP.NET Core backend solution structure with Domain, Application, Infrastructure, Api, Worker, UnitTests, and IntegrationTests projects.
- API baseline with OpenAPI document generation, ProblemDetails, health checks, HTTPS redirection, controller routing, and a simple root status endpoint.
- Documented future API versioning, OpenAPI document segmentation, protected API documentation, production logging, and middleware direction.
- Root status endpoint and health endpoint integration tests using `WebApplicationFactory`.
- xUnit v3 test package baseline.
- Milestone 2 backend foundation learning notes covering project structure, API middleware, OpenAPI, logging, health checks, and integration testing decisions.
- Milestone documentation practice requiring learning notes for each future milestone.
- Milestone 3 - Dependency Registration And Architecture Guards.
- Shared `AddApplication()` and `AddInfrastructure()` dependency-registration extension methods.
- API and Worker startup wiring through the shared Application and Infrastructure registration methods.
- Architecture-boundary unit test coverage for current Clean Architecture project references.
- Integration test coverage proving Application and Infrastructure dependency registration can be composed.
- Milestone 3 learning notes covering dependency-registration setup, architecture guards, TDD, and deferred business scope.
- Planned Milestone 4 - Application Use Case Foundation direction for practical CQRS, MediatR, FluentValidation, optional Moq test doubles, later domain events/outbox, and event sourcing as a future option only.
- Architecture decision record for Application use case patterns.
- Milestone 4 - Application Use Case Foundation implementation with the first submission intake slice.
- `Submission` domain model with draft, submitted, and withdrawn statuses.
- Application-layer `ISubmissionRepository` interface for submission persistence needs.
- MediatR command/result/handler for creating draft submissions.
- FluentValidation validator and MediatR validation pipeline behavior for command/query validation before handlers run.
- Temporary in-memory Infrastructure submission repository so hosts can compose before PostgreSQL is introduced.
- Moq-based handler unit test coverage for repository interaction.
- Unit test coverage for submission domain behavior and create-submission validation.
- `POST /api/v1/submissions` endpoint that dispatches `CreateSubmissionCommand` through MediatR.
- Integration test coverage for successful submission creation and validation failure responses.
- Milestone 4 learning notes covering practical CQRS, Moq placement, temporary in-memory persistence, and Unit of Work deferral.
