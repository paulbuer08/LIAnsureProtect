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
| `/api/v1/health/ready` | "Should traffic come here?" | `DbContextHealthCheck<T>` × 4 — Submission, Notifications, Underwriting, and Claims contexts each `CanConnectAsync`. Any failure → not ready → load balancer routes around the instance. |

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
| Realtime hint failure | `NotificationOutboxMessageConsumer` warning event 4101 | Redis/SignalR is advisory. The committed inbox and outbox progress remain intact; focus/reconnect repairs the UI. Alert only on sustained failures or reconnect storms. |
| Connection-pool budget | shared `NpgsqlDataSource` per API/Worker process | API max 40 + Worker max 20 by default. Multiply each maximum by replica count and retain migration/operations headroom below the database limit. |
| ProblemDetails everywhere | `AddProblemDetails` + controller helpers | Errors are RFC-7807 JSON with correlation ID available on the response header. |

## Resiliency & hardening (M44)

Three capabilities keep the API fast and safe under load:

| Capability | Where | What it does |
|---|---|---|
| **Caching** | `ICacheService` (`Platform.Abstractions.Caching`) → `InMemoryCacheService` (Local) / `RedisCacheService` (Aws) | Cache-aside for **rebuildable, non-PII** data. A query opts in by implementing `ICacheableRequest` (kernel marker: key + TTL); the `CachingBehavior` MediatR behavior then serves it from cache. `RemoveAsync` is the invalidation hook. **Adopted in production:** (1) the evidence reference-data query (`GET /api/v1/evidence-requests/reference`, versioned key, 1h TTL); (2) the **underwriting referral queue** (M44.5): `ListQuoteReferralsQuery` cached shared for **10s** under `underwriting:referral-queue:v1`, with read-your-writes preserved by an API-edge invalidation filter on every queue-affecting write controller — made safe by write-side optimistic concurrency landing first. Per-user/PII reads stay uncached. |
| **Rate limiting** | `Program.cs` global `PartitionedRateLimiter` | Fixed-window per caller (authenticated user id, client-IP fallback), **stricter for unsafe methods**. Over the limit → **429** with `ProblemDetails` + `Retry-After`. Limits come from `RateLimitingOptions` (read per request), generous by default, tightened in production via `RateLimiting:*` config. |
| **Security headers** | `SecurityHeadersMiddleware` | Every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, a locked-down `Content-Security-Policy`, and a restrictive `Permissions-Policy`. |

## Customer-safe failures and developer diagnostics

A failure now has two audiences. `GlobalExceptionHandler` and `SafeProblemDetailsFilter` make the HTTP
contract safe; the shared React `apiClient` validates that contract with Zod and maps stable codes to
actionable copy. A customer never needs to interpret raw JSON, exception types, stack traces, provider
responses, or database messages. Unexpected failures use generic retry guidance and include the
correlation ID as a support ID when available.

Developers still receive the diagnostic trail. `RequestOutcomeMiddleware` records the route template
(not a concrete customer/resource URL), method, status class, duration, and correlation ID under a
stable event ID. Unhandled exceptions use a separate error event ID. Production/Aws hosts write JSON to
stdout so the future EKS collector can forward it without making CloudWatch a request-time dependency.
Native counters/histograms provide request volume and duration without user IDs or error messages as
high-cardinality dimensions.

The browser has a disabled-by-default sanitized telemetry bridge. Terraform/deployment must provide the
official CloudWatch RUM client, allowlisted origins, least-privilege identity, sampling, retention, and
privacy controls before setting `VITE_CLOUDWATCH_RUM_ENABLED=true`. RUM is a regression detector; it is
not domain audit history.

The complete production collection, alarm, privacy, and support-ID procedure is in
[`production-observability-and-customer-errors-runbook.md`](../dev/production-observability-and-customer-errors-runbook.md).

The cache adapter follows the same **Local ⇄ AWS profile switch** as S3 (M42) and SNS (M43): local
in-memory vs Redis (ElastiCache), chosen by `Platform:Profile`, fail-fast on a missing Redis
connection. Redis now starts in the default Compose dependency set because it also carries SignalR
invalidation hints between the separate Worker and API processes. It contains no durable notification
or business payload.

Npgsql's built-in connection pooling is now explicit rather than accidental. One data source per host
serves all four DbContexts, with validated minimum/maximum, acquisition/command timeouts, idle pruning,
and maximum connection lifetime. Inspect `db.client.connections.*` Npgsql meters and PostgreSQL
`pg_stat_activity` (the `Application Name` distinguishes API/Worker). Pool exhaustion, acquisition
latency, active/idle counts, and database saturation are operational signals; passwords and full
connection strings must never enter logs. RDS Proxy/PgBouncer is justified only by measured connection
storms or replica scaling—not by slow queries.

At the AWS edge, CloudFront and ALB must forward WebSocket upgrade headers, allow the configured SPA
origin/credentials, and set an idle timeout above the expected keepalive/reconnect window. Logs must
redact `access_token` query values. Monitor hub connections, reconnects, Redis failures, and query
invalidation rate without adding user IDs or subject IDs as metric dimensions.

## Verifying it locally

```
GET  /api/v1/health/live      → 200 "Healthy"
GET  /api/v1/health/ready     → 200 once Docker PostgreSQL is up (503 otherwise)
GET  / (any request)          → response carries X-Correlation-ID + security headers
POST (flood beyond the limit) → 429 Too Many Requests (ProblemDetails + Retry-After)
```

`HealthEndpointTests` and `OutboxDispatcherTests` pin the observability behavior (correlation echo,
sanitization, generated IDs, probe routes, dispatcher metrics emission, retry/poison metadata,
batch isolation); `SecurityAndRateLimitingEndpointTests` and the `Caching` tests pin the M44
hardening (headers present, 429 on flood, cache hit/miss/invalidation).
