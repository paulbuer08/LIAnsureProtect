# CI/CD Flow

This document records the expected build and deployment flow for LIAnsureProtect.

## Local Script Mapping

Use this script when preparing a local developer machine or validating a branch before continuing work:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

It runs the fresh setup and validation sequence:

```text
stop/remove existing dependency containers
  -> remove local DB volume
  -> pull PostgreSQL/pgvector image if missing
  -> start Docker Compose dependencies
  -> restore NuGet packages
  -> build the solution
  -> restore dotnet tools
  -> apply committed EF Core migrations
  -> run tests
  -> exit
```

Use this script when you want to run the API after setup:

```powershell
.\scripts\dev-up.ps1
```

It runs the same fresh setup path and then starts the API. Because it ends with `dotnet run`, it keeps running until the API is stopped.

Use this script when you want a CI-like one-command verification run with timestamped test results and an optional zip artifact:

```powershell
.\scripts\run-local-ci.ps1
```

By default, it removes the PostgreSQL container and local database volume after the run. Use this option when you want to inspect the database afterward:

```powershell
.\scripts\run-local-ci.ps1 -PostgreSqlAfterRun LeaveRunning
```

Use the smaller scripts only when debugging one step:

- `scripts/start-dependencies.ps1`
- `scripts/update-database.ps1`
- `scripts/stop-dependencies.ps1`

## CI Flow

The CI pipeline should be non-interactive and fail fast.

Recommended first CI flow:

```text
checkout repository
  -> restore .NET SDK and repo-local tools
  -> stop/remove any previous disposable dependencies
  -> remove disposable database volume
  -> start disposable Docker Compose dependencies
  -> restore NuGet packages
  -> build solution
  -> apply EF Core migrations to the disposable PostgreSQL/pgvector database
  -> run tests
  -> publish test/build artifacts
  -> stop Docker Compose dependencies
```

The local setup scripts use checked command execution so CI receives a failed exit code when any Docker or .NET step fails.

CI should publish the timestamped zip artifact produced by `run-local-ci.ps1`. The zip contains the timestamped result folder:

```text
TestResults/local-ci-yyyyMMdd-HHmmss.zip
```

Endpoint integration tests use SQLite in-memory for speed, and migration tests verify PostgreSQL migration SQL.

The project now also has an opt-in PostgreSQL-backed persistence test. CI should enable it after the PostgreSQL/pgvector Compose service is started and migrations have been applied:

```text
LIANSUREPROTECT_RUN_POSTGRES_TESTS=true
LIANSUREPROTECT_TEST_POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=liansureprotect;Username=postgres;Password=postgres
```

The local equivalent is:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

## CD Flow

The CD pipeline should not run `dev-up.ps1`.

Recommended deployment flow:

```text
build immutable application artifact
  -> provision or update infrastructure
  -> apply database migrations as a gated deployment step
  -> deploy API and worker artifacts
  -> run health checks and smoke tests
  -> shift traffic when checks pass
```

Database migrations should run before the new app version receives traffic. In production, migrations should be reviewed for backward compatibility so the old and new app versions can tolerate the database during rolling or blue/green deployment.

## Messaging Direction

Do not add Kafka to the local stack by default.

The current AWS messaging direction is:

```text
transactional outbox
  -> SNS topic
  -> SQS queues
  -> Worker consumers
```

Use EventBridge later when the project needs rule-based routing across AWS services, SaaS integrations, or multiple bounded contexts.

Use Amazon MSK only if a future requirement specifically needs Apache Kafka compatibility, Kafka ecosystem tooling, very high-volume stream processing, or replayable stream consumers. That is not required for the current milestone.
