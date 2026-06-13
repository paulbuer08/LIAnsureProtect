# Dependency Management

This project keeps dependency versions in a small number of predictable places.

## NuGet Packages

NuGet package versions are centralized in:

```text
Directory.Packages.props
```

Project files still say which packages they need, but they do not repeat versions.

Example:

```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
```

The version for that package lives in `Directory.Packages.props`.

This keeps package upgrades intentional. When a future milestone upgrades EF Core, MediatR, FluentValidation, Moq, or xUnit, start with `Directory.Packages.props` instead of searching every project file.

## .NET Tools

Repo-local .NET tools are centralized in:

```text
.config/dotnet-tools.json
```

Milestone 5 uses this for `dotnet-ef`.

Restore tools with:

```powershell
dotnet tool restore
```

The migration script already does this before running EF Core migrations.

## Containerized Service Dependencies

Local service dependencies are defined in:

```text
docker-compose.yml
```

Current service dependency:

| Dependency | Docker Image | Purpose |
| --- | --- | --- |
| PostgreSQL with pgvector | `pgvector/pgvector:0.8.2-pg16-trixie` | Main relational database and future vector storage foundation |

Do not install PostgreSQL locally for this project. Use Docker Compose.

The PostgreSQL image can be overridden with:

```powershell
$env:LIANSUREPROTECT_POSTGRES_IMAGE = "pgvector/pgvector:0.8.2-pg16-trixie"
```

The same variable is used by `docker-compose.yml` and `scripts/setup-dev.ps1`. If the variable is not set, both use the current committed default.

`.env.example` records the default local image value. A developer can copy it to `.env` for Docker Compose, but PowerShell scripts read the process environment variable directly.

## PostgreSQL Library

The .NET PostgreSQL provider is:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

That package lets EF Core talk to PostgreSQL. PostgreSQL itself still runs in a Docker container.

## Authentication Library

The API uses:

```text
Microsoft.AspNetCore.Authentication.JwtBearer
```

That package lets ASP.NET Core validate JWT access tokens sent with:

```http
Authorization: Bearer eyJ...
```

Keep its version in `Directory.Packages.props` with the other Microsoft package versions. The API project should reference the package without repeating the version.

## Test Database Rule

Normal integration tests keep using SQLite in-memory for fast HTTP pipeline checks.

PostgreSQL-specific behavior is covered by an opt-in test:

```text
tests/LIAnsureProtect.IntegrationTests/PostgreSqlPersistenceTests.cs
```

Run it through the setup script:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

That path starts PostgreSQL/pgvector, applies migrations, enables the PostgreSQL test flag, and then runs tests.
