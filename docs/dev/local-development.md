# Local Development

This project should be runnable locally before AWS is introduced.

## Required Tools

- .NET SDK 10
- Docker Desktop
- Git

Later milestones also need:

- Node.js/npm or a Node container workflow for the React milestone.
- AWS CLI or AWS tooling containers for cloud milestones.
- Terraform or Terraform/OpenTofu container workflow for infrastructure milestones.

## Current Tool Notes

The current machine has .NET 10 and Docker available.

Project dependencies should run through Docker Compose instead of relying on manually installed local services. This applies to databases and to future non-database dependencies such as cache, local AWS emulation, and mail services.

Package and tool versions are centralized so future upgrades are easier to find:

- NuGet package versions: `Directory.Packages.props`
- repo-local .NET tools: `.config/dotnet-tools.json`
- local service dependencies: `docker-compose.yml`

See [Dependency Management](dependency-management.md) for the detailed rule.

## Local Services

Docker Compose now runs:

- PostgreSQL with pgvector support

Docker Compose should later add:

- Redis when caching is introduced
- DynamoDB Local when notification inbox/read-model work starts
- LocalStack when local AWS workflow testing is useful
- MailHog or smtp4dev when email workflows exist

The backend may run directly on the host during early development for easier debugging, but its service dependencies should be containerized.

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
.\scripts\run-local-ci.ps1
.\scripts\setup-dev.ps1
.\scripts\dev-up.ps1
```

The first health endpoint is:

```text
/api/v1/health
```

OpenAPI is currently intended for development use. Production access rules will be added later with authentication and role-based authorization.

Detailed setup reasoning and milestone learnings are captured in [Milestone 2 Backend Foundation Learnings](milestone-2-backend-foundation-learnings.md).

## Local PostgreSQL And Migrations

Milestone 5 - Persistence Foundation introduces EF Core persistence for submissions.

The API and Worker read this development connection string name:

```text
ConnectionStrings:LIAnsureProtect
```

The current development connection string points at the PostgreSQL container exposed on localhost:

```text
Host=localhost;Port=5432;Database=liansureprotect;Username=postgres;Password=postgres
```

This is a local-development placeholder for the Docker Compose dependency, not a production secret pattern. Production and cloud environments should provide connection strings through secure configuration such as environment variables, AWS Secrets Manager, or Parameter Store.

Start the dependency stack:

```powershell
.\scripts\start-dependencies.ps1
```

Apply EF Core migrations:

```powershell
.\scripts\update-database.ps1
```

The migration script suppresses EF Core database command logs by default so a fresh database does not show the misleading `__EFMigrationsHistory` lookup failure. To debug raw EF command output, run:

```powershell
.\scripts\update-database.ps1 -SuppressEfCommandLogs:$false
```

Start with a fresh local database, restore/build, apply migrations, and run the API in one command:

```powershell
.\scripts\dev-up.ps1
```

Run the same fresh setup sequence without tests or starting the API:

```powershell
.\scripts\setup-dev.ps1
```

Run `setup-dev.ps1` after a fresh clone, after pulling changes that modify packages, Docker Compose, appsettings, or migrations, or before validating a branch locally. Use `dev-up.ps1` when you want the API to keep running at the end.

By default, `setup-dev.ps1` and `dev-up.ps1` stop and remove the involved Compose stack and remove the local PostgreSQL volume. That gives local development a clean database created from committed EF Core migrations.

Before resetting the local database, the setup script checks that committed migration files exist under `src\LIAnsureProtect.Infrastructure\Persistence\Migrations`. If they are missing, the script stops early with the manual recovery steps: restore the repo-local `dotnet-ef` tool with `dotnet tool restore`, then run `dotnet ef migrations add ...`.

Useful options:

```powershell
.\scripts\run-local-ci.ps1
.\scripts\run-local-ci.ps1 -RunSmokeTests:$false
.\scripts\run-local-ci.ps1 -PostgreSqlAfterRun LeaveRunning
.\scripts\setup-dev.ps1 -RunTests:$true
.\scripts\setup-dev.ps1 -RunApi:$true
.\scripts\setup-dev.ps1 -ResetContainers:$false -RemoveLocalDbVolume:$false
.\scripts\dev-up.ps1 -RunTests:$true
```

When `RemoveLocalDbVolume` is false, the setup script skips automatic migration application. Run `.\scripts\update-database.ps1` manually if you preserve the local database and still need to apply newly pulled migrations.

The scripts fail fast. If Docker, restore, build, migration, or tests fail, the script stops at that step instead of continuing to the next command.

Stop dependencies:

```powershell
.\scripts\stop-dependencies.ps1
.\scripts\stop-dependencies.ps1 -RemoveVolumes:$true
```

The first migration creates the `submissions` table and enables the PostgreSQL `vector` extension. pgvector is included now because later AI/RAG work is expected to store embeddings in PostgreSQL rather than introducing a separate vector database by default.

Current endpoint integration tests replace PostgreSQL with SQLite in-memory for fast request pipeline tests. A migration test also verifies that the generated PostgreSQL migration script creates the `vector` extension and the `submissions` table.

There is also an opt-in PostgreSQL-backed integration test. It verifies the real PostgreSQL/pgvector database has the `vector` extension and can persist a `Submission` through EF Core/Npgsql.

Run it through the setup script:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

Plain `dotnet test` skips that PostgreSQL-backed test unless `LIANSUREPROTECT_RUN_POSTGRES_TESTS=true` is set. This keeps normal test runs fast while still giving us a real database path when the Compose stack is running.

When tests run through `.\scripts\setup-dev.ps1 -RunTests:$true`, `.trx` files are written under `TestResults/`.

When tests run through `.\scripts\run-local-ci.ps1`, results are grouped by run:

```text
TestResults/local-ci-yyyyMMdd-HHmmss/
TestResults/local-ci-yyyyMMdd-HHmmss.zip
```

By default, `run-local-ci.ps1` removes the source result folder after the zip artifact is successfully created. The zip contains the timestamped folder. If zipping fails, or if `-CreateZipArtifact:$false` is used, the source folder remains for inspection.

By default, `run-local-ci.ps1` removes the PostgreSQL container and local database volume after verification. Use `-PostgreSqlAfterRun LeaveRunning` if you want the database to remain available after the checks.

## CI/CD Direction

The local `setup-dev.ps1` script mirrors the first CI pipeline shape:

```text
stop/remove existing dependency containers
  -> remove local DB volume
  -> pull PostgreSQL/pgvector image if missing
  -> start dependencies
  -> restore
  -> build
  -> apply migrations
  -> optionally test
  -> optionally run API
  -> exit
```

Deployment pipelines should not run `dev-up.ps1` because that script starts the API as a foreground process. CD should build an immutable artifact, apply reviewed migrations as a gated step, deploy the API and Worker, then run health checks and smoke tests.

Detailed CI/CD notes are captured in [CI/CD Flow](ci-cd-flow.md).

For a complete step-by-step runbook with diagrams, smoke tests, and troubleshooting, see [Run The App](run-the-app.md).

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
