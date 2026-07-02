# Milestone 41 - Observability - Learning Notes

## Goal

Milestone 41 added the first production-shaped observability baseline before the project introduces S3
document storage and SNS/SQS messaging.

The milestone deliberately stayed local and dependency-light:

- no CloudWatch account wiring;
- no OpenTelemetry collector;
- no X-Ray, Datadog, Prometheus, or Grafana backend;
- no AWS credentials;
- no schema migration.

Instead, it added the signals those later tools can subscribe to:

```text
HTTP correlation
ASP.NET Core liveness/readiness endpoints
structured logs
System.Diagnostics.ActivitySource
System.Diagnostics.Metrics.Meter
```

That gives the local modular monolith useful visibility now and keeps the AWS observability backend as
a later infrastructure concern.

## What changed

### Shared observability names

The Platform shared-kernel contracts now expose stable observability names:

```text
src/Platform/LIAnsureProtect.Platform.Abstractions/Observability/ObservabilityNames.cs
```

That file centralizes:

- API and Worker service names;
- the shared `ActivitySource` name;
- the shared `Meter` name;
- the `X-Correlation-ID` header name;
- dispatcher metric names.

The important part is stability. Exporters, dashboards, runbooks, alarms, and tests need names that do
not drift every time code moves between modules.

### HTTP correlation

The API now has a host-edge middleware:

```text
src/LIAnsureProtect.Api/Observability/RequestCorrelationMiddleware.cs
```

For each request it:

1. Reads `X-Correlation-ID` if the caller supplied one.
2. Generates a new opaque id if the header is missing.
3. Writes the final value back to the response.
4. Sets `HttpContext.TraceIdentifier`.
5. Opens a structured logging scope with correlation id, trace id, request path, and method.

This stays at the API edge. Application handlers and modules do not know about HTTP headers.

## Liveness and readiness

The old health route stays stable:

```text
GET /api/v1/health
```

M41 adds explicit probe routes:

```text
GET /api/v1/health/live
GET /api/v1/health/ready
```

Liveness is intentionally shallow. It answers:

```text
Is this process up?
```

Readiness is dependency-aware. It answers:

```text
Can this instance safely receive traffic?
```

The readiness route checks all three current EF Core contexts:

- `SubmissionDbContext`;
- `NotificationsDbContext`;
- `UnderwritingDbContext`.

The generic readiness check lives in the API observability folder rather than Infrastructure:

```text
src/LIAnsureProtect.Api/Observability/DbContextHealthCheck.cs
```

That was a useful correction during implementation. ASP.NET Core health-check contracts are a host-edge
concern, and keeping the check in API avoids adding ASP.NET health-check package references to the
Infrastructure class library.

## Dispatcher visibility

The local outbox dispatcher is currently the most important asynchronous seam. It drains multiple
sources, merge-orders messages, runs registered consumers, and records retry/poison metadata.

M41 adds visibility without changing that behavior:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcherDiagnostics.cs
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs
```

The dispatcher now emits:

- an activity named `OutboxDispatcher.DispatchPending`;
- per-message activities named `OutboxDispatcher.ProcessMessage`;
- structured logs for batch start, batch completion, and message failures;
- counters for batches, pending messages, processed messages, and failed messages;
- a histogram for dispatch duration in milliseconds.

The metric names are:

```text
liansureprotect.outbox.dispatch.batches
liansureprotect.outbox.dispatch.pending_messages
liansureprotect.outbox.dispatch.processed_messages
liansureprotect.outbox.dispatch.failed_messages
liansureprotect.outbox.dispatch.duration_ms
```

The metrics avoid high-cardinality tags. Message ids, user ids, quote ids, storage keys, and payload
JSON are not emitted as metric dimensions.

## Tests that mattered

The focused health/correlation tests prove:

- a request with `X-Correlation-ID` gets the same value back;
- a request without the header gets a generated value back;
- `/api/v1/health`, `/api/v1/health/live`, and `/api/v1/health/ready` all return success when test
  database contexts are wired to in-memory SQLite.

The dispatcher tests now include a `MeterListener` assertion proving the processed-message counter
emits when a row is dispatched. The existing dispatcher tests still prove ordering, retry, poison,
projection, publishing, and custom consumer behavior.

No dispatcher assertion was weakened.

## What remains later

M41 is instrumentation, not the observability backend.

Later AWS milestones should decide the collector/exporter shape:

```text
ActivitySource / Meter / ILogger
  -> OpenTelemetry SDK/exporter
  -> CloudWatch / X-Ray / Datadog / another backend
```

The later infrastructure work should also add:

- log formatting and retention policy;
- trace sampling policy;
- health probes in container/Kubernetes manifests;
- dashboard and alert definitions;
- SNS/SQS DLQ visibility when Milestone 43 adds real async messaging;
- S3 storage and document pipeline visibility when Milestone 42 adds the S3 adapter.

## Verification

Fresh verification for M41 closeout:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```

Results:

- build passed with 0 warnings and 0 errors;
- focused health/correlation tests passed with 6 tests;
- focused `OutboxDispatcherTests` passed with 16 tests;
- full solution tests passed with UnitTests 66 and IntegrationTests 130, with one PostgreSQL opt-in
  test skipped outside the Docker-backed local CI path;
- all three EF Core pending-model checks reported no pending model changes;
- full local CI passed against fresh Docker PostgreSQL, all three context migration sets, backend
  tests, frontend install/build/lint/tests, artifact creation, and Docker cleanup;
- local CI artifact: `TestResults\local-ci-20260702-164310.zip`.

## Next milestone

The recommended next milestone is:

```text
Milestone 42 - Documents To S3
```

That should replace the local private document byte-storage adapter with an S3-shaped adapter and keep
the existing evidence document metadata, scan trust state, and clean-only download gates intact.
