# Run The App

This guide explains how to run everything that exists in LIAnsureProtect up to `Milestone 5 - Persistence Foundation`.

At this point, the runnable app is:

- ASP.NET Core API
- PostgreSQL with pgvector through Docker Compose
- EF Core migrations for the submissions table
- Unit and integration tests

Not included yet:

- React frontend
- Authentication and authorization
- Redis cache
- AWS services
- Background Worker jobs
- AI/RAG features

Those are intentionally separate milestones.

## Current Runtime Shape

The API runs on the development machine for easy debugging. The application dependencies run through Docker Compose so developers do not manually install PostgreSQL, pgvector, Redis, or future service dependencies.

Today there is one Docker Compose service:

| Service | Container | Purpose |
| --- | --- | --- |
| PostgreSQL with pgvector | `liansureprotect-postgres` | Main relational database and future vector storage foundation |

```mermaid
flowchart LR
    Dev["Developer machine"] --> Script["PowerShell scripts"]
    Script --> Compose["Docker Compose"]
    Compose --> Pg["PostgreSQL plus pgvector container"]
    Script --> Api["ASP.NET Core API"]
    Api --> Pg
```

Think of Docker Compose as the local power strip for dependencies. When the app needs a supporting service, such as a database or future cache, Compose turns it on in a predictable way.

## Prerequisites

Install these before running the app:

- Git
- Docker Desktop
- .NET SDK 10

Docker Desktop must be running before the scripts can start PostgreSQL.

The current backend runs directly with `dotnet run`. That is intentional for this milestone because it keeps debugging simple. Future milestones can add API and Worker containers when the project needs full app container orchestration.

## One Command To Run The API

From the repository root:

```powershell
.\scripts\dev-up.ps1
```

This is the main local run command. By default it creates a fresh local database by stopping and removing the involved Compose stack and removing the PostgreSQL Docker volume before startup.

It runs this sequence:

```mermaid
flowchart TD
    Start["Run .\\scripts\\dev-up.ps1"]
    Reset["Stop/remove Compose stack and local DB volume"]
    Pull["Pull PostgreSQL/pgvector image when missing"]
    Deps["Start Docker Compose dependencies"]
    Restore["Restore NuGet packages"]
    Build["Build solution"]
    Tools["Restore repo-local dotnet tools"]
    Migrate["Apply EF Core migrations"]
    Run["Run ASP.NET Core API"]
    Stop["Keep running until you stop the API"]

    Start --> Reset --> Pull --> Deps --> Restore --> Build --> Tools --> Migrate --> Run --> Stop
```

In plain English:

1. It stops and removes the involved Docker Compose containers.
2. It removes the local PostgreSQL Docker volume.
3. It pulls the PostgreSQL/pgvector image if the image is not already downloaded.
4. It starts PostgreSQL with pgvector.
5. It restores .NET packages.
6. It builds the solution.
7. It restores the repo-local `dotnet-ef` tool.
8. It applies committed EF Core migrations to PostgreSQL.
9. It starts the API.

The script blocks at the final step because the API process keeps running. Press `Ctrl+C` in that terminal to stop the API.

## One Command To Verify Everything

From the repository root:

```powershell
.\scripts\run-local-ci.ps1
```

This is the main one-time verification command. It runs the fresh setup path, applies migrations, runs all tests including the PostgreSQL-backed test, validates Docker Compose config, starts the API briefly, smoke-tests the root endpoint, health endpoint, and submission endpoint, then stops the API.

By default, it also removes the PostgreSQL container and local database volume after verification. That makes the script behave like CI: run the checks, keep the results, and remove disposable test infrastructure.

During the run, it writes one timestamped result folder:

```text
TestResults/local-ci-yyyyMMdd-HHmmss/
```

It also creates a zip artifact by default:

```text
TestResults/local-ci-yyyyMMdd-HHmmss.zip
```

The result folder contains:

- `.trx` test result files
- `api-smoke-result.json`
- `api-smoke-stdout.log`
- `api-smoke-stderr.log`
- `verification-summary.json`

When the zip artifact is created successfully, the script removes the source result folder so the zip is the single artifact to keep or upload. If zip creation fails, the source result folder remains for inspection. If `-CreateZipArtifact:$false` is used, the source result folder remains.

Current `run-local-ci.ps1` flags:

| Flag | Default | Meaning |
| --- | --- | --- |
| `-RunSmokeTests` | `$true` | Starts the API briefly and checks root, health, and create-submission endpoints. |
| `-ApiStartupTimeoutSeconds` | `60` | How long to wait for the API to become healthy during smoke tests. |
| `-PostgreSqlAfterRun` | `Cleanup` | Use `Cleanup` to remove the PostgreSQL container and volume after verification, or `LeaveRunning` to keep it for local development. |
| `-CreateZipArtifact` | `$true` | Creates a zip file from the timestamped result folder. |
| `-ResultsRoot` | `TestResults` | Root folder where verification result folders and zip artifacts are written. |

Useful examples:

```powershell
.\scripts\run-local-ci.ps1 -RunSmokeTests:$false
```

That skips the temporary API startup and only verifies setup, migrations, tests, and Docker Compose config.

```powershell
.\scripts\run-local-ci.ps1 -PostgreSqlAfterRun LeaveRunning
```

That keeps PostgreSQL running after verification so you can continue local development.

```powershell
.\scripts\run-local-ci.ps1 -CreateZipArtifact:$false
```

That keeps the timestamped folder and skips zip creation.

## Setup Without Running The API

Use this command when you want to set up a fresh local database and build the repo but do not want tests or the API to run:

```powershell
.\scripts\setup-dev.ps1
```

It runs this sequence by default:

```mermaid
flowchart TD
    Start["Run .\\scripts\\setup-dev.ps1"]
    Reset["Stop/remove Compose stack and local DB volume"]
    Pull["Pull PostgreSQL/pgvector image when missing"]
    Deps["Start Docker Compose dependencies"]
    Restore["Restore NuGet packages"]
    Build["Build solution"]
    Tools["Restore repo-local dotnet tools"]
    Migrate["Apply EF Core migrations"]
    Exit["Exit"]

    Start --> Reset --> Pull --> Deps --> Restore --> Build --> Tools --> Migrate --> Exit
```

To include tests:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

When tests run through this script, `.trx` test result files are written under `TestResults/`.

To run the API from the same setup script:

```powershell
.\scripts\setup-dev.ps1 -RunApi:$true
```

To preserve the existing local containers and database volume:

```powershell
.\scripts\setup-dev.ps1 -ResetContainers:$false -RemoveLocalDbVolume:$false
```

When `RemoveLocalDbVolume` is false, the setup script does not apply migrations automatically. If you preserve the database but still need to update schema after pulling migration changes, run:

```powershell
.\scripts\update-database.ps1
```

When `RemoveLocalDbVolume` is true, the setup script first checks that committed EF Core migration files exist under:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Migrations
```

If the folder is missing or contains only the model snapshot, the script stops before touching Docker and prints the manual recovery steps. The scripts intentionally do not create migrations automatically because migration files should be reviewed and committed like normal source code.

Run `setup-dev.ps1`:

- After a fresh clone
- After pulling package changes
- After pulling Docker Compose changes
- After pulling migration changes
- Before committing or opening a pull request

## Step By Step Manual Flow

Use the individual scripts when debugging one part of setup.

### 1. Start Dependencies

```powershell
.\scripts\start-dependencies.ps1
```

This starts Docker Compose and waits for PostgreSQL to become healthy. It does not remove existing containers or volumes. Use `setup-dev.ps1` when you want the default fresh reset behavior.

Check the service:

```powershell
docker compose ps
```

Expected result: the `postgres` service is running and healthy.

### 2. Restore And Build

```powershell
dotnet restore LIAnsureProtect.slnx
dotnet build LIAnsureProtect.slnx --no-restore
```

Restore downloads NuGet packages. Build compiles the API, Application, Domain, Infrastructure, Worker, and test projects.

### 3. Apply Migrations

```powershell
.\scripts\update-database.ps1
```

This restores the repo-local `dotnet-ef` tool and applies the EF Core migration to PostgreSQL.

By default, this script suppresses EF Core database command logs during `dotnet ef database update`. That keeps fresh-database setup logs clean because EF Core can otherwise log a misleading failed query while checking for `__EFMigrationsHistory` before the table exists.

To see raw EF Core database command logs while debugging migrations:

```powershell
.\scripts\update-database.ps1 -SuppressEfCommandLogs:$false
```

The first migration:

- Enables the PostgreSQL `vector` extension
- Creates the `submissions` table

### 4. Run Tests

```powershell
dotnet test LIAnsureProtect.slnx --no-build
```

Current tests include:

- Domain behavior tests
- Application command handler tests
- Validation tests
- Dependency registration tests
- Submission endpoint tests
- Migration SQL tests for pgvector and the submissions table
- Opt-in PostgreSQL persistence test for pgvector and real EF Core/Npgsql persistence

The PostgreSQL-backed test is skipped during a plain `dotnet test` run. Use this command when you want the real database test included:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

Test result files are created when tests run through `setup-dev.ps1 -RunTests:$true` or `run-local-ci.ps1`:

```text
TestResults/*.trx
```

### 5. Run The API

```powershell
dotnet run --project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj
```

The development launch profile exposes:

```text
http://localhost:5223
https://localhost:7167
```

Use the HTTP URL first if the local HTTPS development certificate is not trusted.

## Smoke Test The Running API

After `dev-up.ps1` starts the API, open a second terminal from the repository root.

Check the root status endpoint:

```powershell
Invoke-RestMethod http://localhost:5223/
```

Expected shape:

```json
{
  "application": "LIAnsureProtect.Api",
  "status": "Running"
}
```

Check health:

```powershell
Invoke-RestMethod http://localhost:5223/api/v1/health
```

Expected response:

```text
Healthy
```

Create a draft submission:

```powershell
$body = @{
    applicantName = "Jane Applicant"
    applicantEmail = "jane@example.com"
    companyName = "Example Company"
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri http://localhost:5223/api/v1/submissions `
    -ContentType "application/json" `
    -Body $body
```

Expected shape:

```json
{
  "submissionId": "generated-guid",
  "status": "Draft"
}
```

## Request Workflow

This is what happens when the API receives `POST /api/v1/submissions`:

```mermaid
sequenceDiagram
    participant Client
    participant Controller as SubmissionsController
    participant MediatR
    participant Validation as ValidationBehavior
    participant Handler as CreateSubmissionCommandHandler
    participant Domain as Submission domain model
    participant Repository as EfCoreSubmissionRepository
    participant Db as SubmissionDbContext
    participant Uow as EfCoreUnitOfWork
    participant Pg as PostgreSQL

    Client->>Controller: POST /api/v1/submissions
    Controller->>MediatR: Send CreateSubmissionCommand
    MediatR->>Validation: Validate command
    Validation->>Handler: Continue when valid
    Handler->>Domain: CreateDraft(...)
    Handler->>Repository: AddAsync(submission)
    Repository->>Db: Stage submission
    Handler->>Uow: SaveChangesAsync()
    Uow->>Db: SaveChangesAsync()
    Db->>Pg: INSERT submission
    Controller-->>Client: 201 Created
```

The important idea is that the repository stages the change and Unit of Work commits it. This keeps the Application layer from depending directly on EF Core.

## Stop The App

Stop the API process with `Ctrl+C`.

Stop Docker Compose dependencies:

```powershell
.\scripts\stop-dependencies.ps1
```

This stops and removes the Compose containers. The named Docker volume keeps local database data unless you pass `-RemoveVolumes:$true`:

```powershell
.\scripts\stop-dependencies.ps1 -RemoveVolumes:$true
```

## Troubleshooting

### Docker Daemon Is Not Running

Symptom:

```text
failed to connect to the docker API at npipe:////./pipe/docker_engine
```

Fix:

1. Start Docker Desktop.
2. Wait until Docker reports that it is running.
3. Rerun `.\scripts\setup-dev.ps1` or `.\scripts\dev-up.ps1`.

### Docker Config Access Warning

Symptom:

```text
WARNING: Error loading config file: open C:\Users\Poy\.docker\config.json: Access is denied.
```

Meaning:

Docker can render Compose config, but the current process cannot read the user Docker config file.

Fix:

- Run from a normal user shell that has access to Docker Desktop.
- If the warning continues, check permissions on `C:\Users\Poy\.docker\config.json`.

### Raw EF Core Logs Show A Failed Migration History Query On A Fresh Database

Symptom when running raw `dotnet ef database update` or `update-database.ps1 -SuppressEfCommandLogs:$false`:

```text
Failed executing DbCommand ...
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
```

Meaning:

On a fresh database, EF Core checks whether the migration history table exists before applying migrations. If the script continues and prints `Applying migration ...` followed by `Done.`, the setup succeeded.

Fix:

- No action is needed when the migration completes successfully.
- Use the default `update-database.ps1` behavior for normal local setup and CI logs. It suppresses this EF command-log noise while still failing on real migration errors through the `dotnet ef` exit code.
- Investigate only if the script stops with an exception after that message.

### Migration Files Are Missing

Symptom:

```text
EF Core migration files are missing.
Expected folder:
src\LIAnsureProtect.Infrastructure\Persistence\Migrations
```

Meaning:

The setup and local CI scripts apply committed EF Core migrations. They do not create new migration files. If migration files are gone, a fresh PostgreSQL database cannot be built from source.

Fix:

Restore the repo-local `dotnet-ef` tool first:

```powershell
dotnet tool restore
```

Then create the first migration:

```powershell
dotnet ef migrations add CreateSubmissionPersistence `
  --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj `
  --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj `
  --context SubmissionDbContext `
  --output-dir Persistence\Migrations
```

The script prints these recovery commands as normal console output before throwing a short failure. Copy the command from that normal output, not from PowerShell's red exception-rendering block.

Then rerun:

```powershell
.\scripts\run-local-ci.ps1
```

### NuGet Restore Fails With NU1301

Symptom:

```text
NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json
```

Meaning:

The shell cannot reach NuGet. In a restricted sandbox this can happen even when the code is valid.

Fix:

1. Check internet access.
2. Check proxy or firewall rules.
3. Run `dotnet restore LIAnsureProtect.slnx` from a normal development terminal.
4. Rerun `.\scripts\setup-dev.ps1`.

### Port 5432 Is Already In Use

Symptom:

PostgreSQL container cannot bind to localhost port `5432`.

Fix options:

- Stop the process already using port `5432`.
- Or change the published port in `docker-compose.yml` and update the development connection string.

Keep the internal container port as `5432`; only the host-side published port should change.

## CI/CD Shape

Local setup and CI should follow the same order:

```mermaid
flowchart LR
    Checkout["Checkout"]
    Deps["Start disposable dependencies"]
    Restore["Restore"]
    Build["Build"]
    Migrate["Migrate database"]
    Test["Test"]
    Publish["Publish artifacts"]

    Checkout --> Deps --> Restore --> Build --> Migrate --> Test --> Publish
```

CI should use disposable containers and fail fast. CD should not run `dev-up.ps1` because that command starts a foreground API process. Deployment should build an artifact, apply reviewed migrations as a gated deployment step, deploy the app, then run smoke tests.

## Current Milestone Boundary

Do not add these to the local run path yet unless a future milestone approves them:

- Redis
- Kafka
- LocalStack
- React frontend
- Authentication
- Domain events or outbox
- Event sourcing

The expected future messaging direction is AWS-native:

```mermaid
flowchart LR
    App["Application"]
    Outbox["Transactional outbox"]
    Sns["SNS topic"]
    Sqs["SQS queues"]
    Worker["Worker consumers"]

    App --> Outbox --> Sns --> Sqs --> Worker
```

Kafka is not part of the default plan. If a future requirement specifically needs Kafka compatibility or stream replay semantics, Amazon MSK can be considered then.
