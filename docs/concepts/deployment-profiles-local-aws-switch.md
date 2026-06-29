# The Local ⇄ AWS Deploy Switch (`Platform:Profile`)

## The idea in one sentence

A single configuration value, `Platform:Profile`, chooses which set of infrastructure **adapters**
the app wires up at startup — `Local` for your machine, `Aws` for the cloud — so the **same code and
the same container image** run everywhere; only the plugged-in equipment differs.

## The analogy

Same **car**, two **fuel types**. A flex-fuel engine runs on either petrol or ethanol; you don't
rebuild the engine, you just fill a different tank. `Platform:Profile` is that fuel selector for the
application: flip it and the engine draws from Local or AWS "tanks" (filesystem vs S3, in-process
messaging vs SNS/SQS, Postgres-in-Docker vs Aurora).

## How it works

```text
appsettings / env var
   Platform:Profile = "Local"   (default)   or   "Aws"
            │
            ▼
   PlatformProfileResolver.Resolve(configuration)   ──►  PlatformProfile.Local / .Aws
            │
            ▼
   Program.cs (composition root)
     builder.Services.AddPlatform(configuration);            // clock, options, shared kernel
     builder.Services.AddInfrastructure(conn, profile);      // picks adapters by profile
            │
            ▼
   per port:  Local → LocalAdapter        Aws → AwsAdapter (added milestone by milestone)
```

- **Default is `Local`.** A missing or empty value means Local, so developers need zero config.
- **Unknown values fail fast.** `Platform:Profile=Azure` throws at startup with a clear message —
  a typo can never silently run the wrong adapters.
- **Not-yet-built AWS adapters fail fast too.** Until an AWS adapter ships, selecting `Aws` for that
  port throws a dated message (e.g. *"AWS document storage adapter arrives in Milestone 42"*) instead
  of pretending to work.

## The port → adapter map (target)

| Concern (port) | Local adapter | AWS adapter | Lands in |
|---|---|---|---|
| Object storage | local filesystem | S3 (+SSE-KMS, presigned URLs) | M42 |
| Messaging | in-process / LocalStack | SNS + SQS (+DLQ) + EventBridge | M40 |
| Database | Postgres (Docker) | Aurora PostgreSQL (pgvector) | M44 |
| Cache | in-memory / local Redis | ElastiCache (Redis) | M41 |
| Secrets/config | User Secrets / appsettings | Secrets Manager / SSM | M43 |
| Identity | Auth0 / local test | Auth0 *or* Cognito | M46 |

## Why we use it

- **One image, many environments** — fewer "works on my machine" surprises; what you test locally is
  what runs in the cloud, minus the adapter swap.
- **Incremental cloud migration** — we light up AWS one port at a time without forking the codebase.
- **Cost control** — AWS adapters only exist/are selected when we deliberately run in the cloud; the
  default keeps everything local and free.

## How it shows up in this codebase (Milestone 32)

- `PlatformProfile` enum and `PlatformOptions` live in `LIAnsureProtect.Platform.Abstractions`.
- `PlatformProfileResolver` and `AddPlatform(...)` live in `LIAnsureProtect.Platform`.
- The **first proven port** is document storage: `AddInfrastructure(conn, profile)` registers
  `LocalDocumentStorageService` for `Local` and fails fast for `Aws`.
- Covered by `tests/LIAnsureProtect.IntegrationTests/Platform/PlatformProfileSwitchTests.cs`.

## Setting it

```jsonc
// appsettings.json (or environment variable Platform__Profile=Aws)
{
  "Platform": {
    "Profile": "Local"   // "Local" (default) or "Aws"
  }
}
```
