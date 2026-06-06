# Local Development

This project should be runnable locally before AWS is introduced.

## Required Tools

- .NET SDK 10
- Docker Desktop
- Node.js and npm
- Git

Later milestones also need:

- AWS CLI
- Terraform

## Current Tool Notes

The current machine has .NET 10 and Docker available.

Node.js/npm, AWS CLI, and Terraform still need to be installed or fixed in PATH before frontend and cloud milestones.

## Local Services Planned

Docker Compose will eventually run:

- PostgreSQL
- Redis
- DynamoDB Local
- LocalStack
- MailHog or smtp4dev

The backend and frontend may run directly on the host during early development for easier debugging.

## Backend Foundation

Milestone 2 creates the backend solution using this structure:

```text
src/
  LIAnsureProtect.Domain/
  LIAnsureProtect.Application/
  LIAnsureProtect.Infrastructure/
  LIAnsureProtect.Api/
  LIAnsureProtect.Worker/
tests/
  LIAnsureProtect.UnitTests/
  LIAnsureProtect.IntegrationTests/
```

The API project is the local HTTP entry point. The Worker project is a placeholder for future background processing.

Useful local commands from the repository root:

```powershell
dotnet build LIAnsureProtect.slnx
dotnet test LIAnsureProtect.slnx
dotnet run --project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj
```

The first health endpoint is:

```text
/api/v1/health
```

OpenAPI is currently intended for development use. Production access rules will be added later with authentication and role-based authorization.

Detailed setup reasoning and milestone learnings are captured in [Milestone 2 Backend Foundation Learnings](milestone-2-backend-foundation-learnings.md).

## Observability Direction

The first logging baseline uses the built-in ASP.NET Core logging abstractions. In local development, logs appear in Visual Studio output or the terminal.

For AWS ECS/Fargate, prefer writing structured logs to console first and letting the container platform ship logs to CloudWatch. Add Serilog or a CloudWatch-specific provider later only when the project needs richer log formatting, enrichment, or sink control.

## Development Rule

Work milestone by milestone.

For each milestone:

1. Explain the design.
2. Create or update the smallest useful set of files.
3. Run the relevant verification command.
4. Update docs and changelog.
5. Add or update a milestone learning notes document.
6. Commit only after the milestone is stable.

Every milestone should preserve important questions, tradeoffs, mistakes, fixes, and production-minded decisions in a dedicated learning notes document. The practice is described in [Milestone Documentation Practice](milestone-documentation-practice.md).
