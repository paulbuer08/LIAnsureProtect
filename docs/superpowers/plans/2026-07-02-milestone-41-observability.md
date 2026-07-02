# Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a local, production-shaped observability baseline for API requests, health/readiness, and the outbox dispatcher without changing business behavior.

**Architecture:** Platform owns shared observability names. The API owns HTTP correlation and health endpoint mapping. Infrastructure owns dispatcher-specific logs, activities, and metrics because the local dispatcher lives there. The milestone uses native .NET diagnostics so OpenTelemetry and AWS exporters can be added later without forcing cloud dependencies into this local slice.

**Tech Stack:** .NET 10, ASP.NET Core health checks and middleware, EF Core `Database.CanConnectAsync`, `ILogger`, `System.Diagnostics.ActivitySource`, `System.Diagnostics.Metrics.Meter`, xUnit integration tests.

---

## File Structure

- Create `src/Platform/LIAnsureProtect.Platform.Abstractions/Observability/ObservabilityNames.cs`
  - Shared service, activity source, meter, metric, and correlation header names.
- Create `src/LIAnsureProtect.Api/Observability/RequestCorrelationMiddleware.cs`
  - API host-edge middleware for `X-Correlation-ID` and structured request log scopes.
- Modify `src/LIAnsureProtect.Api/Program.cs`
  - Add correlation middleware, explicit live/ready health routes, and readiness checks.
- Create `src/LIAnsureProtect.Infrastructure/Health/DbContextHealthCheck.cs`
  - Generic EF Core readiness check used by the API composition root.
- Create `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcherDiagnostics.cs`
  - Native metric/activity helper for the local dispatcher.
- Modify `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs`
  - Add `ILogger<OutboxDispatcher>`, activities, metrics, and structured logs.
- Modify `tests/LIAnsureProtect.IntegrationTests/HealthEndpointTests.cs`
  - Add correlation and liveness/readiness coverage using SQLite-backed test contexts.
- Modify `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`
  - Add a `MeterListener` assertion proving dispatcher processed-message metrics emit.
- Create closeout docs after implementation:
  - `docs/dev/milestone-41-observability-learnings.md`
- Modify closeout/status docs after implementation:
  - `docs/project-status.md`
  - `docs/architecture/overview.md`
  - `docs/dev/production-transformation-roadmap.md`
  - `README.md`
  - `CHANGELOG.md`

## Task 1: Add Planning Docs

**Files:**
- Create: `docs/dev/milestone-41-observability-design.md`
- Create: `docs/superpowers/plans/2026-07-02-milestone-41-observability.md`

- [ ] **Step 1: Verify M41 docs do not already exist**

Run:

```powershell
Test-Path docs/dev/milestone-41-observability-design.md
Test-Path docs/superpowers/plans/2026-07-02-milestone-41-observability.md
```

Expected before this task: both print `False`.

- [ ] **Step 2: Add the design and implementation plan docs**

The design must lock these scope decisions:

```text
Native .NET diagnostics first.
No telemetry backend or AWS dependency in M41.
No business route changes.
No database schema changes.
No module-boundary loosening.
```

- [ ] **Step 3: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 4: Commit**

Run:

```powershell
git add -- docs/dev/milestone-41-observability-design.md docs/superpowers/plans/2026-07-02-milestone-41-observability.md
git commit -m "docs: start observability milestone"
```

## Task 2: Add Correlation And Health Readiness

**Files:**
- Create: `src/Platform/LIAnsureProtect.Platform.Abstractions/Observability/ObservabilityNames.cs`
- Create: `src/LIAnsureProtect.Api/Observability/RequestCorrelationMiddleware.cs`
- Create: `src/LIAnsureProtect.Infrastructure/Health/DbContextHealthCheck.cs`
- Modify: `src/LIAnsureProtect.Api/Program.cs`
- Modify: `tests/LIAnsureProtect.IntegrationTests/HealthEndpointTests.cs`

- [ ] **Step 1: Add shared observability names**

Create `ObservabilityNames.cs`:

```csharp
namespace LIAnsureProtect.Platform.Abstractions.Observability;

public static class ObservabilityNames
{
    public const string ServiceName = "LIAnsureProtect";
    public const string ApiServiceName = "LIAnsureProtect.Api";
    public const string WorkerServiceName = "LIAnsureProtect.Worker";
    public const string ActivitySourceName = "LIAnsureProtect";
    public const string MeterName = "LIAnsureProtect";
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    public const string OutboxDispatchBatchesMetric = "liansureprotect.outbox.dispatch.batches";
    public const string OutboxDispatchPendingMessagesMetric = "liansureprotect.outbox.dispatch.pending_messages";
    public const string OutboxDispatchProcessedMessagesMetric = "liansureprotect.outbox.dispatch.processed_messages";
    public const string OutboxDispatchFailedMessagesMetric = "liansureprotect.outbox.dispatch.failed_messages";
    public const string OutboxDispatchDurationMetric = "liansureprotect.outbox.dispatch.duration_ms";
}
```

- [ ] **Step 2: Add request correlation middleware**

Create `RequestCorrelationMiddleware.cs`:

```csharp
using LIAnsureProtect.Platform.Abstractions.Observability;

namespace LIAnsureProtect.Api.Observability;

public sealed class RequestCorrelationMiddleware(
    RequestDelegate next,
    ILogger<RequestCorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[ObservabilityNames.CorrelationIdHeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString(),
            ["RequestPath"] = context.Request.Path.Value,
            ["RequestMethod"] = context.Request.Method
        });

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ObservabilityNames.CorrelationIdHeaderName, out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return Guid.NewGuid().ToString("N");
    }
}
```

- [ ] **Step 3: Add generic DbContext readiness check**

Create `DbContextHealthCheck.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LIAnsureProtect.Infrastructure.Health;

public sealed class DbContextHealthCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{typeof(TContext).Name} cannot connect.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"{typeof(TContext).Name} readiness check failed.", exception);
        }
    }
}
```

- [ ] **Step 4: Wire middleware and health routes**

In `Program.cs`, add `using` statements for API observability, Infrastructure health, module DbContexts,
and health check options. Register checks:

```csharp
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DbContextHealthCheck<SubmissionDbContext>>("submission-db", tags: ["ready", "database"])
    .AddCheck<DbContextHealthCheck<NotificationsDbContext>>("notifications-db", tags: ["ready", "database"])
    .AddCheck<DbContextHealthCheck<UnderwritingDbContext>>("underwriting-db", tags: ["ready", "database"]);
```

Add middleware before auth:

```csharp
app.UseMiddleware<RequestCorrelationMiddleware>();
```

Keep the old route and add explicit routes:

```csharp
app.MapHealthChecks("/api/v1/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/api/v1/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/api/v1/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

- [ ] **Step 5: Add integration coverage**

In `HealthEndpointTests.cs`, configure SQLite-backed contexts for readiness and add tests:

```csharp
[Fact]
public async Task Request_With_Correlation_Header_Echoes_Correlation_Header()

[Fact]
public async Task Request_Without_Correlation_Header_Returns_Generated_Correlation_Header()

[Theory]
[InlineData("/api/v1/health")]
[InlineData("/api/v1/health/live")]
[InlineData("/api/v1/health/ready")]
public async Task Health_Routes_Return_Success(string requestUri)
```

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test LIAnsureProtect.slnx --no-build --filter FullyQualifiedName~HealthEndpointTests
```

Expected: health/correlation tests pass after building in the next step.

- [ ] **Step 7: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 8: Commit**

Run:

```powershell
git add -- src/Platform/LIAnsureProtect.Platform.Abstractions/Observability/ObservabilityNames.cs src/LIAnsureProtect.Api/Observability/RequestCorrelationMiddleware.cs src/LIAnsureProtect.Infrastructure/Health/DbContextHealthCheck.cs src/LIAnsureProtect.Api/Program.cs tests/LIAnsureProtect.IntegrationTests/HealthEndpointTests.cs
git commit -m "feat(observability): add correlation and readiness checks"
```

## Task 3: Add Dispatcher Logs, Activities, And Metrics

**Files:**
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcherDiagnostics.cs`
- Modify: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs`
- Modify: `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`

- [ ] **Step 1: Add dispatcher diagnostics helper**

Create `OutboxDispatcherDiagnostics.cs` with:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using LIAnsureProtect.Platform.Abstractions.Observability;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

internal static class OutboxDispatcherDiagnostics
{
    public static readonly ActivitySource ActivitySource = new(ObservabilityNames.ActivitySourceName);
    public static readonly Meter Meter = new(ObservabilityNames.MeterName);

    public static readonly Counter<long> Batches =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchBatchesMetric);
    public static readonly Counter<long> PendingMessages =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchPendingMessagesMetric);
    public static readonly Counter<long> ProcessedMessages =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchProcessedMessagesMetric);
    public static readonly Counter<long> FailedMessages =
        Meter.CreateCounter<long>(ObservabilityNames.OutboxDispatchFailedMessagesMetric);
    public static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>(ObservabilityNames.OutboxDispatchDurationMetric, "ms");
}
```

- [ ] **Step 2: Add dispatcher logging and metrics**

Change the dispatcher constructor to include:

```csharp
ILogger<OutboxDispatcher> logger
```

Wrap dispatch with:

```csharp
using var activity = OutboxDispatcherDiagnostics.ActivitySource.StartActivity("OutboxDispatcher.DispatchPending");
var stopwatch = Stopwatch.StartNew();
```

Record counters and duration after the batch:

```csharp
OutboxDispatcherDiagnostics.Batches.Add(1);
OutboxDispatcherDiagnostics.PendingMessages.Add(pendingMessages.Count);
OutboxDispatcherDiagnostics.ProcessedMessages.Add(processedCount);
OutboxDispatcherDiagnostics.FailedMessages.Add(failedCount);
OutboxDispatcherDiagnostics.DurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);
```

Add structured logs for batch completion and failure without logging payloads.

- [ ] **Step 3: Add dispatcher metric test**

In `OutboxDispatcherTests.cs`, add a `MeterListener` test that listens for
`ObservabilityNames.OutboxDispatchProcessedMessagesMetric`, dispatches one processable outbox row, and
asserts the observed value is at least `1`.

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet test LIAnsureProtect.slnx --no-build --filter FullyQualifiedName~OutboxDispatcherTests
```

Expected: all dispatcher tests pass after building in the next step.

- [ ] **Step 5: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add -- src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcherDiagnostics.cs src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs
git commit -m "feat(observability): instrument outbox dispatcher"
```

## Task 4: Closeout Docs

**Files:**
- Create: `docs/dev/milestone-41-observability-learnings.md`
- Modify: `docs/project-status.md`
- Modify: `docs/architecture/overview.md`
- Modify: `docs/dev/production-transformation-roadmap.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add learning notes**

Capture:

```text
why M41 used native diagnostics before exporters;
how correlation works;
how liveness/readiness differ;
what dispatcher metrics/logs expose;
what stays deferred to AWS/OpenTelemetry backend milestones.
```

- [ ] **Step 2: Update status docs**

Set:

```text
Latest closed milestone: Milestone 41 - Observability.
Current milestone: Milestone 41 - Observability, complete on branch feat/milestone-41-observability.
Recommended next milestone after M41: Milestone 42 - Documents to S3.
```

- [ ] **Step 3: Update architecture and roadmap**

Add an observability section that explains:

```text
HTTP correlation;
health/live and health/ready;
native ActivitySource/Meter dispatcher instrumentation;
CloudWatch/OpenTelemetry exporters deferred to AWS infrastructure milestones.
```

- [ ] **Step 4: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- docs/dev/milestone-41-observability-learnings.md docs/project-status.md docs/architecture/overview.md docs/dev/production-transformation-roadmap.md README.md CHANGELOG.md
git commit -m "docs: close observability milestone"
```

## Final Verification

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```

Expected:

```text
build: 0 Warning(s), 0 Error(s)
tests: pass, with the PostgreSQL opt-in test skipped unless explicitly enabled
EF: no pending model changes for all three contexts
local CI: Local CI passed.
```
