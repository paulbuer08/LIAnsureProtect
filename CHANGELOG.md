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
