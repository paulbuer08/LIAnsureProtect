# Milestone 3 - Dependency Registration And Architecture Guards Learnings

This document records the decisions and tradeoffs from Milestone 3 - Dependency Registration And Architecture Guards.

## Why This Milestone Exists

Milestone 2 created the backend foundation. Milestone 3 keeps that foundation clean before adding business workflows.

The two goals are:

- give Application and Infrastructure their own dependency-registration entry points
- add a small architecture guard so project references do not drift accidentally

Simple analogy:

- `Program.cs` is the host's front desk checklist.
- `AddApplication()` is the button that sets up the business workflow layer.
- `AddInfrastructure()` is the button that sets up the toolbox layer.

Right now those buttons do not register real business services because no real services exist yet. That is intentional. The milestone creates the right place for future registrations without inventing fake code.

## Dependency Registration Decision

Application now exposes:

```csharp
services.AddApplication();
```

Infrastructure now exposes:

```csharp
services.AddInfrastructure();
```

The API and Worker hosts both call those methods.

Why this helps:

- `Program.cs` stays readable as the system grows.
- Future Application use cases can be registered inside the Application project.
- Future Infrastructure implementations can be registered inside the Infrastructure project.
- API and Worker can share the same setup path instead of duplicating registrations.

## Why The Methods Are Empty For Now

The methods currently return `services` without adding custom services.

That is not a mistake. There are no real Application or Infrastructure services yet.

Adding fake services would make the project look busier without adding useful behavior. The better production habit is to create the extension points now and add real registrations only when real services exist.

## Architecture Guard Decision

Milestone 3 adds a unit test that reads the production `.csproj` files and verifies the expected project-reference direction:

```text
Domain
  no production project references

Application
  references Domain

Infrastructure
  references Application
  references Domain

Api
  references Application
  references Infrastructure

Worker
  references Application
  references Infrastructure
```

This is a small guardrail. If someone later adds a project reference that breaks the intended direction, the test fails.

## Why The Guard Reads Project Files

There are architecture-testing libraries that can inspect assemblies and namespaces. Those may be useful later.

For this milestone, reading `.csproj` files is enough because the first risk is simple: project references drifting in the wrong direction.

This keeps the test:

- easy to understand
- package-light
- directly tied to the current architecture rule

## TDD Notes

The registration test was written before implementation.

The first test run failed because the `LIAnsureProtect.Application` and `LIAnsureProtect.Infrastructure` extension namespaces did not exist yet. That was the expected red state.

After adding the extension methods and wiring API and Worker startup, the tests passed.

The architecture-boundary test passed immediately because the current project references already followed the intended direction. It still adds value because it locks in that rule for future changes.

## NuGet Restore Note

Adding dependency-registration extension methods required `Microsoft.Extensions.DependencyInjection.Abstractions` in Application and Infrastructure.

The first restore attempt failed inside the restricted sandbox because NuGet signature metadata required network access to `api.nuget.org`.

The command was rerun with approved network access, packages restored, and tests passed.

## What Was Intentionally Not Added

Milestone 3 did not add:

- authentication
- authorization policies
- CORS
- database schema
- EF Core DbContext
- repositories
- business endpoints
- React frontend
- cloud infrastructure

Those belong in later milestones after their scope is approved.

## What To Remember

Use `AddApplication()` for Application-layer services and use cases.

Use `AddInfrastructure()` for external-service implementations such as EF Core, PostgreSQL, Redis, S3, SNS/SQS, DynamoDB, and local document storage.

Keep API and Worker startup focused on host-level concerns and call layer registration methods for shared setup.

When the first business slice begins, add real unit tests for actual Domain or Application behavior instead of adding placeholder tests.
