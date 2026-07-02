# Chapter 11 — Observability & Operations

**Question this chapter answers:** when something misbehaves at 2 a.m., how do we *see* it —
and how do orchestrators keep the system healthy without a human?

> **Analogy:** observability is the hospital wristband + monitors. Every patient (request) gets
> a wristband ID at the door (correlation ID); monitors (metrics) show vital signs; the chart
> (structured logs) records everything with the wristband ID on every line; and the ward nurse
> (Kubernetes) checks pulse (liveness) and "ready for visitors?" (readiness) on a schedule.

## Correlation — following one request through everything

`RequestCorrelationMiddleware` (`src/LIAnsureProtect.Api/Observability/`):

1. Reads `X-Correlation-ID` from the request — the value is **sanitized against a strict
   allowlist** (alphanumerics, `-`, `_`, `.`, max 64 chars; a client can never inject newlines
   into logs or headers). Missing/unsafe → a fresh GUID.
2. Sets `HttpContext.TraceIdentifier`, echoes the header on the response (so the frontend or a
   support ticket can quote it).
3. Opens a **log scope** carrying `CorrelationId`, `TraceId` (from `Activity.Current`), sanitized
   `RequestPath` and `RequestMethod` — every log line inside the request inherits them.

Names live in `Platform.Abstractions.Observability.ObservabilityNames`, shared by API and Worker.

## Health probes — liveness vs readiness

| Route | Question | Checks |
|---|---|---|
| `/api/v1/health/live` (and legacy `/api/v1/health`) | "Is the process up?" | `self` only — no dependencies, so a database outage never gets the pod killed. |
| `/api/v1/health/ready` | "Should traffic come here?" | `DbContextHealthCheck<T>` × 3 — Submission, Notifications, Underwriting contexts each `CanConnectAsync`. Any failure → not ready → load balancer routes around the instance. |

The health check itself is fail-safe: exceptions become `Unhealthy` results, never crashes.

## Dispatcher diagnostics (the part most worth watching)

`OutboxDispatcherDiagnostics` publishes, under `ObservabilityNames`:

- **Traces:** an `ActivitySource` span per dispatch batch (`OutboxDispatcher.DispatchPending`,
  tags: source/pending/processed/failed counts) and per message (`ProcessMessage`, tags: type,
  attempt count, outcome).
- **Metrics (`Meter`):** counters for batches, pending, processed, failed messages + a batch
  duration histogram — the four numbers that tell you if the event spine is healthy
  (growing *pending* + flat *processed* = investigate).
- **Structured logs:** batch summaries, per-failure warnings with the poison/exhausted flag,
  consumer exception errors.

Everything uses native .NET APIs, so wiring OpenTelemetry exporters (→ CloudWatch/X-Ray/Datadog)
in the AWS phase is configuration, not rework.

## Operational behaviors to know

| Behavior | Where | What to expect |
|---|---|---|
| Worker survives transient failures | `Worker.ExecuteAsync` catch-log-retry | A DB restart shows error logs, then normal polling resumes. Host only stops on real shutdown. |
| Poison messages | `outbox_messages.failed_at_utc IS NOT NULL` | Parked with error text after 3 attempts — query this when a notification "never arrived". |
| Idempotency cleanup | Worker, hourly | Completed records >7 days deleted; log line reports the count. |
| Startup fail-fast | `Program.cs` guards | Missing/`http://` Auth authority, missing audience, unknown platform profile, or an AWS adapter not yet implemented all refuse to boot with a clear message — misconfiguration is loud, not silent. |
| ProblemDetails everywhere | `AddProblemDetails` + controller helpers | Errors are RFC-7807 JSON with correlation ID available on the response header. |

## Verifying it locally

```
GET  /api/v1/health/live      → 200 "Healthy"
GET  /api/v1/health/ready     → 200 once Docker PostgreSQL is up (503 otherwise)
GET  / (any request)          → response carries X-Correlation-ID
```

`HealthEndpointTests` and `OutboxDispatcherTests` pin all of this behavior (correlation echo,
sanitization, generated IDs, probe routes, dispatcher metrics emission, retry/poison metadata,
batch isolation).
