# Milestone 41 - Observability - Design

## Goal

Milestone 41 adds production-grade visibility around the modular monolith before the project introduces
S3 document storage and SNS/SQS messaging.

The milestone should make the local API, Worker, module database boundaries, and outbox dispatcher
easier to operate without changing business workflows, public business routes, database schemas, or
module ownership rules.

The target shape is:

```text
HTTP request
  -> correlation id / trace id scope
  -> structured request logs
  -> liveness and readiness health surfaces

OutboxDispatcher
  -> structured batch/message logs
  -> ActivitySource spans around dispatch batches/messages
  -> Meter counters/histograms for pending, processed, failed, and duration
```

## Why this milestone exists

Milestones 37 through 40 made the dispatcher and module boundaries more realistic:

- Underwriting now owns evidence request/review/document data and a module outbox.
- The dispatcher drains more than one outbox source and merge-orders rows by `CreatedAtUtc`.
- Dispatcher side effects are registered consumers instead of dispatcher-owned mapper calls.
- Quoting decision authority is explicit, but quote persistence still remains in the legacy
  `SubmissionDbContext` until a later carve.

The next roadmap milestones add production infrastructure:

- Milestone 42 moves document storage to S3.
- Milestone 43 introduces real async messaging through SNS/SQS and DLQs.

Before those external dependencies arrive, the local system needs visibility into request correlation,
dependency readiness, and the dispatcher path.

## In scope

- Add project-owned observability names and native .NET diagnostics primitives:
  - service name;
  - API/Worker service names;
  - `ActivitySource` name;
  - `Meter` name;
  - stable metric names.
- Add API request correlation middleware:
  - accept `X-Correlation-ID` when present;
  - generate one when missing;
  - write the correlation id to the response;
  - add correlation id, trace id, route, and method to structured log scopes.
- Keep existing business routes stable.
- Keep `/api/v1/health` stable and add explicit liveness/readiness routes:
  - `/api/v1/health/live`;
  - `/api/v1/health/ready`.
- Add readiness checks for all three EF Core contexts:
  - `SubmissionDbContext`;
  - `NotificationsDbContext`;
  - `UnderwritingDbContext`.
- Add dispatcher visibility:
  - structured logs for batch start/completion and per-message failures;
  - `ActivitySource` spans around dispatch batches and messages;
  - metrics for batches, pending messages, processed messages, failed messages, and dispatch duration.
- Add focused backend tests for:
  - correlation response header behavior;
  - liveness/readiness health routes;
  - dispatcher metric emission.
- Document the new observability baseline and how it prepares the AWS observability track.

## Out of scope

- Adding real CloudWatch, X-Ray, Datadog, Prometheus, Grafana, or collector deployment.
- Adding AWS credentials, Terraform, EKS, S3, SNS, SQS, DLQs, or LocalStack.
- Adding a persisted audit/event table for telemetry.
- Changing public business API routes or React contracts.
- Changing auth/authorization semantics.
- Moving quote/rating/policy tables.
- Adding a new `DbContext` or EF migration.
- Making any module reference another module or legacy layer.

## Design

### Native .NET diagnostics first

M41 should not force a cloud backend into local development. The project can expose useful signals with
the .NET primitives that OpenTelemetry and cloud exporters can later subscribe to:

```text
System.Diagnostics.ActivitySource
System.Diagnostics.Metrics.Meter
ILogger structured logs
ASP.NET Core health checks
```

The shared names live in Platform so both hosts can use the same constants:

```text
LIAnsureProtect.Platform.Abstractions/Observability
  -> ObservabilityNames
```

The dispatcher-specific metric helper can live beside the dispatcher in Infrastructure because it
describes the current local dispatcher implementation.

### Correlation

The API accepts a caller-provided correlation id through:

```text
X-Correlation-ID
```

If the header is absent, the middleware generates a new opaque id. Every response includes exactly one
correlation id header. The middleware also starts a logging scope so later logs in the request path
carry:

```text
CorrelationId
TraceId
RequestPath
RequestMethod
```

This is intentionally host-edge behavior. Application handlers and modules should not know about HTTP
headers.

### Health checks

The existing route remains:

```text
GET /api/v1/health
```

M41 adds explicit operational routes:

```text
GET /api/v1/health/live
GET /api/v1/health/ready
```

Liveness answers "is this process up?" and should not depend on PostgreSQL. Readiness answers "can this
instance serve traffic safely?" and checks the three current database contexts.

```text
ready
  -> SubmissionDbContext can connect
  -> NotificationsDbContext can connect
  -> UnderwritingDbContext can connect
```

This maps cleanly to later container and Kubernetes probes.

### Dispatcher visibility

The dispatcher is the most important asynchronous seam today. It should expose:

- how many source rows were pending in a batch;
- how many were processed;
- how many failed;
- how long dispatch took;
- which source and event type failed, without logging raw payload JSON.

The dispatcher should keep retry and poison behavior unchanged. Observability must wrap the existing
behavior; it must not become part of business correctness.

The activity hierarchy should be:

```text
OutboxDispatcher.DispatchPending
  -> OutboxDispatcher.ProcessMessage
```

The metric names should be stable and low-cardinality:

```text
liansureprotect.outbox.dispatch.batches
liansureprotect.outbox.dispatch.pending_messages
liansureprotect.outbox.dispatch.processed_messages
liansureprotect.outbox.dispatch.failed_messages
liansureprotect.outbox.dispatch.duration_ms
```

Metric tags should avoid high-cardinality values such as message ids, user ids, quote ids, and payload
contents.

## Boundary rules

- Platform may hold generic observability names because API, Worker, Infrastructure, and modules can
  safely depend on Platform shared-kernel contracts.
- API owns HTTP correlation middleware.
- Infrastructure owns dispatcher-specific metrics and logs because the local dispatcher lives there.
- Modules should not reference legacy layers or other modules.
- No module should need to know the HTTP correlation header name.
- No database schema changes are expected.

## Test strategy

Focused tests should prove observable behavior without requiring a telemetry backend:

- request without `X-Correlation-ID` returns a generated `X-Correlation-ID`;
- request with `X-Correlation-ID` echoes that id;
- `/api/v1/health`, `/api/v1/health/live`, and `/api/v1/health/ready` return success when test
  database contexts are wired to in-memory SQLite;
- dispatcher emits the processed-message counter when it processes a row.

Existing outbox tests should continue to prove ordering, retry, poison, projection, and publishing
behavior. Do not weaken those assertions.

## Verification

Per task:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Final gates:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```
