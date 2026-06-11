# Milestone 5 - Persistence Foundation Learnings

This document records the decisions and tradeoffs from Milestone 5 - Persistence Foundation.

## Why This Milestone Exists

Milestone 4 created the first submission intake use case, but it saved submissions through a temporary in-memory repository.

That was useful as a temporary desk tray:

```text
Create a submission
  -> place it in memory
  -> lose it when the process stops
```

Milestone 5 replaces that temporary tray with the real filing cabinet: EF Core backed by PostgreSQL running through Docker Compose.

## What Changed

The create-submission flow now uses this persistence path:

```text
CreateSubmissionCommandHandler
  -> ISubmissionRepository.AddAsync(...)
  -> EfCoreSubmissionRepository
  -> SubmissionDbContext.Submissions.AddAsync(...)
  -> IUnitOfWork.SaveChangesAsync(...)
  -> EfCoreUnitOfWork
  -> SubmissionDbContext.SaveChangesAsync(...)
```

The repository stages the change. Unit of Work commits the change.

Simple analogy:

```text
Repository:
  "Put this form into the filing tray."

Unit of Work:
  "Save everything in the tray to the official filing cabinet."
```

## Why Unit Of Work Belongs Here

Unit of Work was intentionally deferred in Milestone 4 because there was no real database transaction or `SaveChangesAsync` boundary yet.

Milestone 5 adds EF Core, so Unit of Work now has a concrete job:

- coordinate EF Core save behavior
- keep handlers from depending on `DbContext`
- give Application one commit promise

`IUnitOfWork` lives in Application because handlers need the promise.

`EfCoreUnitOfWork` lives in Infrastructure because EF Core is an Infrastructure detail.

## Why The Domain Model Stayed Protected

The `Submission` domain model still controls creation through:

```text
Submission.CreateDraft(...)
```

Milestone 5 did not add public setters just to make EF Core easier. The domain model should keep protecting its rules.

EF Core mapping belongs in Infrastructure, so database concerns adapt to the domain model instead of weakening the domain model for the database.

## Submission Mapping

`SubmissionConfiguration` maps the domain model to the `submissions` table.

Important mapping choices:

- `Id` is not database-generated because the domain model creates the `Guid`.
- `Status` is stored as a string for readability.
- applicant name, applicant email, and company name have explicit maximum lengths.
- column names use snake_case to fit PostgreSQL conventions.

## Dependency Injection

`AddInfrastructure(...)` now requires the `LIAnsureProtect` connection string from the host.

The API and Worker hosts read:

```text
ConnectionStrings:LIAnsureProtect
```

and pass it into Infrastructure.

This keeps host configuration in the host and persistence implementation in Infrastructure.

## Integration Test Database Strategy

The production provider is PostgreSQL through Npgsql.

Local development also uses PostgreSQL through Docker Compose. The project should not require manually installed database services.

The endpoint integration tests use SQLite in-memory. This keeps request pipeline tests fast while Docker Compose remains the local PostgreSQL path.

Important test detail:

`AddDbContext` stores provider configuration in DI. The test host must remove the existing `IDbContextOptionsConfiguration<SubmissionDbContext>` registration before adding SQLite. If it only removes built `DbContextOptions`, EF Core sees both Npgsql and SQLite providers and fails.

## Package Decisions

NuGet package versions are now centralized in:

```text
Directory.Packages.props
```

Project files still choose their packages, but the versions live in one repo-level file. This prevents the same EF Core or testing package family from drifting across Application, Infrastructure, API, Worker, UnitTests, and IntegrationTests.

Infrastructure uses:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

Integration tests use:

```text
Microsoft.EntityFrameworkCore.Sqlite
```

SQLite is for fast endpoint test isolation only. PostgreSQL remains the single system of record for the application.

## Docker Compose And Migrations

Milestone 5 adds Docker Compose for the local dependency stack.

The current dependency stack contains PostgreSQL with pgvector:

```text
pgvector/pgvector:0.8.2-pg16-trixie
```

The repo also includes scripts:

- `scripts/common.ps1`
- `scripts/start-dependencies.ps1`
- `scripts/update-database.ps1`
- `scripts/setup-dev.ps1`
- `scripts/dev-up.ps1`
- `scripts/stop-dependencies.ps1`

The one-command setup path is:

```powershell
.\scripts\setup-dev.ps1
```

That runs the fresh setup path, applies committed migrations, and exits without running tests or the API by default.

After the setup script was refined, its default behavior became fresh local setup: stop/remove the involved Compose stack, remove the local PostgreSQL volume, pull the PostgreSQL/pgvector image only when it is missing, start dependencies, restore/build, and apply committed migrations. Tests and API startup are opt-in flags:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
.\scripts\setup-dev.ps1 -RunApi:$true
```

`common.ps1` contains the shared command wrapper used by the scripts. It turns failed `docker` and `dotnet` exit codes into script failures, which is important because native commands can otherwise print errors without stopping the rest of a PowerShell script.

`update-database.ps1` suppresses EF Core database command logs by default while running `dotnet ef database update`. EF Core can log a failed `__EFMigrationsHistory` query on a fresh database before applying the first migration, which is technically expected but misleading in local CI output. The script still fails on real migration errors because `Invoke-CheckedCommand` checks the `dotnet ef` exit code. Raw EF command logs can be enabled with `-SuppressEfCommandLogs:$false` when debugging migrations.

`setup-dev.ps1` also checks for committed migration files before it resets the local PostgreSQL database. This keeps missing migrations from becoming a confusing database or test failure later in the run. The script intentionally does not create migrations automatically because migration files are schema-changing source files that should be reviewed and committed deliberately. The recovery message includes `dotnet tool restore` first because `dotnet-ef` is a repo-local tool, and it prints the copyable commands as normal console output before throwing a short failure.

The API project also references `Microsoft.EntityFrameworkCore.Design` as a private design-time package. Even though the `SubmissionDbContext` lives in Infrastructure, `dotnet ef database update` uses the API as the startup project so it can build the real host configuration and dependency registration. EF Core tooling therefore needs the design package available from that startup project.

After regenerating the first migration from the current model, the repo-local `dotnet-ef` tool and EF Core package family were aligned on `10.0.9`. Keeping the tool, design package, provider-facing runtime references, and SQLite test provider on the same patch version avoids warning noise and assembly version conflicts during build and migrations.

The one-command local run path is:

```powershell
.\scripts\dev-up.ps1
```

That runs the fresh setup path and runs the API. Tests can be included with:

```powershell
.\scripts\dev-up.ps1 -RunTests:$true
```

`setup-dev.ps1 -RunTests:$true` enables the opt-in PostgreSQL-backed test by setting `LIANSUREPROTECT_RUN_POSTGRES_TESTS=true` and pointing `LIANSUREPROTECT_TEST_POSTGRES_CONNECTION_STRING` at the local Docker Compose PostgreSQL database for the duration of the test run.

`run-local-ci.ps1` is the CI-like verification wrapper. It creates one timestamped result folder per run, writes `.trx` test results and smoke-test output there, creates a zip artifact by default, removes the source folder after the zip is successfully created, and removes the PostgreSQL container plus local database volume unless `-PostgreSqlAfterRun LeaveRunning` is passed.

The PostgreSQL/pgvector image can be overridden with `LIANSUREPROTECT_POSTGRES_IMAGE`. If it is not set, Compose and the setup script use the committed default:

```text
pgvector/pgvector:0.8.2-pg16-trixie
```

## pgvector Direction

pgvector is expected for later AI/RAG work.

The first migration enables the PostgreSQL `vector` extension now:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

This does not add AI behavior yet. It keeps the database ready for future embedding tables while preserving PostgreSQL as the main system of record.

## What Was Intentionally Not Added

Milestone 5 does not add:

- authentication
- authorization policies
- React frontend
- cloud infrastructure
- domain events
- transactional outbox
- event sourcing
- separate read and write databases

Those are separate milestone decisions.

## What To Remember

Keep repository interfaces in Application when handlers need persistence promises.

Keep EF Core and PostgreSQL details in Infrastructure.

Use Unit of Work when there is a real persistence commit boundary.

Do not reintroduce in-memory repositories as the default runtime persistence path.

Do not treat SQLite endpoint tests as the production database decision.

Use Docker Compose for local service dependencies instead of manually installed services.

Do not add Redis, Kafka, or LocalStack before the milestone that needs them. Redis belongs with caching. LocalStack belongs with local AWS integration testing. Kafka is not the default; the planned AWS-native path is transactional outbox to SNS/SQS workers, with EventBridge or Amazon MSK considered only if future requirements justify them.
