# ADR-001: Use Practical Clean Architecture

## Status

Accepted

## Context

LIAnsureProtect needs to grow from a simple Cyber MVP into a broader specialty insurance platform. The app will eventually include database access, document storage, messaging, caching, cloud services, background workers, and AI-assisted workflows.

Putting all logic directly in controllers would make the project hard to test and hard to change.

## Decision

Use practical Clean Architecture:

- Domain contains business concepts and rules.
- Application contains use cases, DTOs, validators, and interfaces.
- Infrastructure implements external concerns such as EF Core, PostgreSQL, Redis, S3, DynamoDB, and messaging.
- Api exposes HTTP endpoints and wires authentication, authorization, and middleware.
- Workers handle background processing.

## Consequences

This adds more projects and folders than a small demo app, but it keeps business rules easier to test and prevents AWS or database code from leaking everywhere.

The goal is not ceremony. The goal is clear boundaries.
