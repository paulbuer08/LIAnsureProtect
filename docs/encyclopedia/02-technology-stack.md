# Chapter 2 — Technology Stack

Every technology below is in the repository **today** (not aspirational), with the reason it was
chosen and exactly how this project uses it. AWS-target services appear at the end, clearly
marked as *planned*.

> **Analogy:** a technology stack is a kitchen. The recipes (Chapters 5–10) matter most, but you
> still want to know which oven, which knives, and why the pantry is organized the way it is.

## Backend

| Technology | Why it was chosen | How this project uses it |
|---|---|---|
| **.NET 10 / C# / ASP.NET Core Web API** | Mature, fast, first-class async, strong typing, huge ecosystem; the team's production target. | Two hosts: `src/LIAnsureProtect.Api` (HTTP API) and `src/LIAnsureProtect.Worker` (background processor). All business code lives in class libraries the hosts compose. |
| **EF Core + Npgsql (PostgreSQL)** | PostgreSQL is the single system of record (ADR-003); EF Core gives migrations, LINQ, change tracking, and transactional saves. | **Three `DbContext`s** — legacy `SubmissionDbContext` (public schema), `NotificationsDbContext` (`notifications` schema), `UnderwritingDbContext` (`underwriting` schema). Each module owns its schema; migrations are applied per-context (`scripts/update-database.ps1`, CI). |
| **MediatR** | Decouples HTTP endpoints from use-case handlers; enables pipeline behaviors (validation) without cluttering controllers. | Every use case is a command/query record + handler (e.g. `CreateSubmissionCommand` → `CreateSubmissionCommandHandler`). Controllers only translate HTTP ⇄ commands. |
| **FluentValidation** | Declarative request validation that runs *before* handlers. | `ValidationBehavior` (a MediatR pipeline behavior) validates every command/query that has a validator; failures become RFC-7807 `400` responses. |
| **Microsoft.Extensions.Http.Resilience (`IHttpClientFactory`)** | Outbound HTTP must not choke the app when a partner is slow — pooled handlers, retry, circuit breaker, timeout out of the box. | The external rating provider adapter is a **typed client**: `AddHttpClient<IRatingProviderClient, RatingProviderHttpClient>(...).AddStandardResilienceHandler(...)` in `src/LIAnsureProtect.Infrastructure/DependencyInjection.cs`. Any future outbound HTTP integration must follow this same pattern. |
| **JWT Bearer authentication (Auth0)** | Delegating identity to a specialist (OIDC provider) beats storing passwords; standard, verifiable tokens. | `Program.cs` trusts only the configured HTTPS issuer + audience; roles come from a configurable role claim. `HttpContextCurrentUser` adapts claims to the `ICurrentUser` port. |
| **Swagger / OpenAPI (`AddOpenApi`)** | Discoverable API contract during development. | Mapped only in the Development environment. |
| **Health checks** | Orchestrators (Kubernetes/EKS) need to know "alive?" vs "ready for traffic?". | `/api/v1/health/live` (process up) and `/api/v1/health/ready` (all three DbContexts can connect) via `DbContextHealthCheck<TContext>`. |
| **System.Diagnostics `ActivitySource` + `Meter`** | Native .NET tracing/metrics — OpenTelemetry-exportable later without rework. | `OutboxDispatcherDiagnostics` publishes spans, counters (batches, pending/processed/failed) and a duration histogram under names in `ObservabilityNames`. |
| **xUnit + Moq + `WebApplicationFactory` + SQLite in-memory** | Fast, deterministic tests: unit tests for domain/handlers, full HTTP-pipeline integration tests without a real network or database server. | `tests/LIAnsureProtect.UnitTests` (66 tests incl. architecture guards), `tests/LIAnsureProtect.IntegrationTests` (real HTTP calls through the middleware/auth pipeline against SQLite-backed contexts; one opt-in PostgreSQL test runs in local CI's Docker path). |

## Frontend

| Technology | Why | How |
|---|---|---|
| **React 19 + TypeScript + Vite** | Modern component model, type safety, instant dev server (ADR-002). | `src/LIAnsureProtect.Web`, feature-owned structure: `features/<feature>/{api,hooks,pages,types}`. |
| **React Router** | Standard client-side routing. | Route table in `App.tsx`; every business route is wrapped in `RequireAuth`. |
| **TanStack Query** | Server state (caching, retries, invalidation) is a solved problem — don't hand-roll it. | Each feature's `hooks/` wraps its `api/` calls in queries/mutations; mutations invalidate the affected queries. |
| **React Hook Form + Zod** | Performant forms + schema-first validation shared between UI and types. | `SubmissionIntakeForm` uses `zodResolver(submissionIntakeSchema)`. |
| **Auth0 React SDK** | Hosted Universal Login: signup, login, MFA — none of it hand-built. | `Auth0Provider` in `main.tsx`; `useAuth0().getAccessTokenSilently()` supplies the bearer token for every API call. |
| **Tailwind CSS** | Utility-first styling without a bespoke design system. | Used across all pages/components. |
| **Vitest + React Testing Library** | Same-speed tests co-located with features. | `*.test.tsx` next to the code they test. |
| **Zustand** (approved, minimal) | Small client-only state without Redux ceremony. | Only where client state is truly client-only (Redux deliberately not used). |

## Data & storage

| Store | Status | Role |
|---|---|---|
| **PostgreSQL (Docker locally)** | In use | The single **system of record**. One database, schema-per-module (`public` legacy, `notifications`, `underwriting`). pgvector image ready for later AI/RAG. |
| **Document storage — local filesystem *and* S3** | In use | Two adapters behind the one `Platform.Abstractions.Documents` port: `LocalDocumentStorageService` (filesystem) under `Platform:Profile=Local`, and `S3DocumentStorageService` (AWS SDK, SSE-KMS) under `Platform:Profile=Aws` (M42). The S3 adapter is developed and round-trip tested against **LocalStack** — no AWS account, no bill. |
| **LocalStack (Docker, opt-in)** | In use (dev/test) | Emulates AWS S3 on `localhost` so the S3 adapter runs with no cloud cost. Profile-scoped compose service (`docker compose --profile aws-local up -d`); the round-trip test is env-gated and skipped in normal CI. |
| **Redis (ElastiCache)** | Planned (M44) | Cache-aside for rebuildable data only — never documents or claim details. |
| **SNS/SQS, DynamoDB** | Planned (M43+) | Real async messaging behind the outbox, notification read-model options. (S3 landed in M42.) |

## Tooling, CI/CD & security automation

| Tool | Role |
|---|---|
| **GitHub Actions CI** | PR gate: backend build + per-context migrations + tests; frontend build/lint/tests. Branch protection requires both. |
| **CodeQL + code-scanning gate** | Static security analysis on every PR; a repository ruleset (`main-code-scanning-gate`) **blocks merging** while any `error`-level CodeQL alert is open on the PR. |
| **Claude PR review, PR labeler, Dependabot** | Automated review comments, path-based labels, dependency update PRs. |
| **Docker Compose** | Local PostgreSQL (pgvector image) with health checks; `scripts/*.ps1` wrap start/migrate/run flows. |
| **Central package management** | `Directory.Packages.props` pins every NuGet version in one place. |
| **Local CI script** | Mirrors the cloud pipeline against a fresh Docker PostgreSQL and archives artifacts under `TestResults/`. |

## Infrastructure-as-Code direction (Phase 2, M45+)

- **Terraform** is the approved IaC tool: remote state, per-environment modules, everything
  `destroy`-able so nothing bills while idle.
- **Ansible** was evaluated (2026-07) as a complement: in this architecture there are **no
  long-lived VMs to configure** — compute is EKS/Fargate containers and managed services — so
  classic Ansible configuration management has little to attach to. Decision: **Terraform stays
  the single IaC tool**; Ansible is *optional* and only becomes relevant if EC2-based components
  (e.g. self-managed runners/bastions) appear, in which case it would own OS-level configuration
  while Terraform keeps owning provisioning. This decision is recorded in the roadmap.
