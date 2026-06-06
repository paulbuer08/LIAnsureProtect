# ADR-004: Establish API Foundation And Versioning Direction

## Status

Accepted

## Context

LIAnsureProtect needs an ASP.NET Core API foundation that is simple enough to learn from but organized enough to grow into production-style workflows.

The project will eventually expose customer, broker, underwriter, claims, admin, document, notification, and AI-assist endpoints. Some endpoints may be public-facing while others should remain internal or role-restricted.

## Decision

Use ASP.NET Core Web API with controllers for business resources.

Use a small Milestone 2 API baseline:

- OpenAPI document generation.
- ProblemDetails for standard API error responses.
- Health checks.
- HTTPS redirection.
- Controller routing.
- A simple root status endpoint.
- `/api/v1/health` as the first versioned operational endpoint.

Use `/api/v1/...` as the public API route prefix from the beginning.

Do not add full API versioning infrastructure yet. Add formal API versioning before a breaking API change, before exposing multiple live API versions, or when business endpoints are mature enough to need separate versioned contracts.

Keep OpenAPI exposed only in development for now. Later, protect OpenAPI access with authentication and role-based authorization before exposing documentation outside local development.

Plan for multiple OpenAPI documents later when needed:

- API versions such as v1 and v2.
- Public versus internal APIs.
- Frontend-consumed versus backend/internal APIs.

Use built-in ASP.NET Core logging first. For AWS ECS/Fargate, prefer console logging collected by the container platform into CloudWatch. Add Serilog or CloudWatch-specific providers later only when richer formatting, enrichment, or sink control is needed.

## Consequences

The API starts with useful production-shaped defaults without adding premature authentication, CORS, database, cloud, or observability complexity.

The route prefix makes future API versions easier to introduce.

OpenAPI is useful for local development immediately, while production exposure remains gated until security exists.

The project keeps flexibility for later public/internal API documents without needing that complexity before real endpoint groups exist.
