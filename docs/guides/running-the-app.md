# Running The App — The Complete Guide

> **Living document.** This guide describes how to run everything that exists **today** (post-M44:
> S3/SNS/Redis adapters via LocalStack/Docker, caching, rate limiting, security headers). It is
> updated in every milestone PR, like the encyclopedia. The old `docs/dev/run-the-app.md` is kept
> only as history (it stops at Milestone 9).

## What you are about to run

LIAnsureProtect has **four moving parts** locally:

| Part | What it is | Where it runs |
|---|---|---|
| **PostgreSQL** (with pgvector) | The system of record — all business data | Docker container (`liansureprotect-postgres`) |
| **API** (`LIAnsureProtect.Api`) | The HTTP backend the browser talks to | Your machine, `dotnet run` — <http://localhost:5223> |
| **Worker** (`LIAnsureProtect.Worker`) | Background loop: drains the outbox (notifications, projections), cleans idempotency records | Your machine, `dotnet run` (optional but recommended) |
| **Web** (`LIAnsureProtect.Web`) | The React UI | Your machine, `npm run dev` — <http://localhost:5173> |

> **Analogy:** PostgreSQL is the **filing room**, the API is the **front desk**, the Worker is the
> **mailroom clerk** who walks the outbox to the post office every few seconds, and the Web app is
> the **customer lobby**. You can open the front desk without the mailroom clerk — but then
> notifications/queue projections sit in the outbox until the clerk shows up.

Optional (only for AWS-adapter development): **LocalStack** (S3 + SNS + SQS emulator) and
**Redis** — one command starts both, and nothing needs them in the default Local profile.

## Prerequisites (one-time)

1. **Git**
2. **Docker Desktop** — must be running before any script
3. **.NET SDK 10** (`global.json` pins the version; `dotnet --version` to check)
4. **Node.js 24+** with npm (matches CI)
5. **An Auth0 tenant (free)** — the app's login system. One-time setup below.

## One-time Auth0 setup (login will not work without this)

The app never stores passwords; Auth0 does the logging-in. You need one free tenant with two
registered applications, one API, five roles, and a small "put roles into the token" action.

### 1. Create the API (the audience)

Auth0 Dashboard → *Applications → APIs → Create API*:

- Name: `LIAnsureProtect API`
- Identifier (**audience**): `https://api.liansureprotect.local`  ← use exactly this
- Signing algorithm: RS256

### 2. Create the SPA application (the React app)

*Applications → Applications → Create Application* → **Single Page Application**:

- Name: `LIAnsureProtect Web`
- Allowed Callback URLs: `http://localhost:5173/callback`
- Allowed Logout URLs: `http://localhost:5173`
- Allowed Web Origins: `http://localhost:5173`

Note its **Domain** (e.g. `dev-yourtenant.us.auth0.com`) and **Client ID**.

### 3. Create the roles

*User Management → Roles → Create Role*, five times:

```text
Customer · Broker · Underwriter · ClaimsAdjuster · Admin
```

(`ClaimsAdjuster` is reserved for the future Claims context — create it now so test users are ready.)

### 4. Add the role claim to access tokens

Auth0 does not put roles into API access tokens by default. *Actions → Library → Create Action*
("Add roles to tokens", trigger: **Login / Post Login**), with this code, then **deploy it and drag
it into the Login flow** (*Actions → Triggers → post-login*):

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = "https://liansureprotect.local/roles";
  if (event.authorization) {
    api.accessToken.setCustomClaim(namespace, event.authorization.roles);
    api.idToken.setCustomClaim(namespace, event.authorization.roles);
  }
};
```

The API is configured to read roles from exactly that claim
(`Authentication:RoleClaimType = https://liansureprotect.local/roles`).

### 5. Create test users

See the [Manual Testing Guide](manual-testing-guide.md) for the recommended generic personas
(one user per role). Short version: *User Management → Users → Create User* (email + password),
then on each user's *Roles* tab assign one role.

### 6. Tell the backend and frontend about your tenant

**Backend** — store the tenant-specific authority in User Secrets (never commit it):

```powershell
dotnet user-secrets set "Authentication:Authority" "https://dev-yourtenant.us.auth0.com/" --project src/LIAnsureProtect.Api
```

The audience and role-claim type are project constants already committed in
`appsettings.Development.json` — you don't need to change them.

**Frontend** — create `src/LIAnsureProtect.Web/.env.local` (gitignored) from the committed
`.env.example`:

```text
VITE_AUTH0_DOMAIN=dev-yourtenant.us.auth0.com
VITE_AUTH0_CLIENT_ID=YOUR_SPA_CLIENT_ID
VITE_AUTH0_AUDIENCE=https://api.liansureprotect.local
VITE_AUTH0_CALLBACK_URL=http://localhost:5173/callback
VITE_API_BASE_URL=http://localhost:5223
```

## Everyday run (three terminals)

### Terminal 1 — database + migrations + API

```powershell
.\scripts\dev-up.ps1
```

This resets the local Docker Postgres (fresh database each time by default), applies the EF Core
migrations for **all three DbContexts** (`SubmissionDbContext`, `NotificationsDbContext`,
`UnderwritingDbContext`), builds, and runs the API at <http://localhost:5223>. Keep it running.

Prefer to keep your existing data? `.\scripts\setup-dev.ps1 -ResetContainers:$false -RemoveLocalDbVolume:$false`
then `.\scripts\update-database.ps1` and run the API manually:

```powershell
dotnet run --project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj
```

### Terminal 2 — the Worker (recommended)

```powershell
dotnet run --project src\LIAnsureProtect.Worker\LIAnsureProtect.Worker.csproj
```

Without it, everything still works **except** what the outbox delivers: notification inboxes and
the underwriting referral queue/timeline projections will lag until the Worker runs (it drains the
outbox every ~5 seconds).

### Terminal 3 — the frontend

```powershell
cd src\LIAnsureProtect.Web
npm install   # first time only; npm ci for the exact locked tree
npm run dev
```

Open <http://localhost:5173>, click **Log in**, sign in with one of your Auth0 test users.

## Quick health checks

```powershell
Invoke-RestMethod http://localhost:5223/                     # → application/status JSON
Invoke-RestMethod http://localhost:5223/api/v1/health/live   # → Healthy (process up)
Invoke-RestMethod http://localhost:5223/api/v1/health/ready  # → Healthy (all 3 DB contexts reachable)
```

Every response also carries `X-Correlation-ID` plus the security headers
(`X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, …) added in M44.

## Full verification (what CI runs)

```powershell
.\scripts\run-local-ci.ps1
```

Backend setup + migrations + all tests + compose validation + API smoke tests + frontend
`npm ci`/build/lint/test. Writes a timestamped artifact under `TestResults/`.

## Optional: the AWS-adapter services (LocalStack + Redis)

Only needed when developing/testing the `Platform:Profile=Aws` adapters (S3 documents, SNS
notifications, Redis cache). They are **profile-scoped** so the default flow never starts them:

```powershell
docker compose --profile aws-local up -d      # starts localstack (S3,SNS,SQS) + redis
```

Then run the opt-in round-trip tests (each skipped by default):

```powershell
$env:LIANSUREPROTECT_RUN_S3_TESTS = "true"     # S3 document round trip
$env:LIANSUREPROTECT_RUN_SNS_TESTS = "true"    # SNS→SQS+DLQ notification round trip
$env:LIANSUREPROTECT_RUN_REDIS_TESTS = "true"  # Redis cache round trip
dotnet test tests/LIAnsureProtect.IntegrationTests/LIAnsureProtect.IntegrationTests.csproj
docker compose --profile aws-local down        # tear down when done
```

See `.env.example` (repo root) for all switches. No AWS account is involved — LocalStack emulates
S3/SNS/SQS on `localhost:4566`.

## Ports and addresses (reference)

| Thing | Address |
|---|---|
| API (HTTP) | `http://localhost:5223` |
| API (HTTPS) | `https://localhost:7167` |
| Web dev server | `http://localhost:5173` |
| PostgreSQL | `localhost:5432` (`postgres`/`postgres`, db `liansureprotect`) |
| LocalStack (opt-in) | `http://localhost:4566` |
| Redis (opt-in) | `localhost:6379` |

## Stopping everything

- API / Worker / Web: `Ctrl+C` in each terminal.
- Dependencies: `.\scripts\stop-dependencies.ps1` (add `-RemoveVolumes:$true` to wipe the database).
- Opt-in services: `docker compose --profile aws-local down`.

## Troubleshooting

The detailed troubleshooting catalog (locked DLLs, port 5432 in use, EF history-table log noise,
`EPERM unlink` on npm, Docker daemon not running) lives in the historical
[docs/dev/run-the-app.md](../dev/run-the-app.md#troubleshooting) and still applies. Auth-specific
issues:

| Symptom | Likely cause |
|---|---|
| Login redirects but the app shows an Auth0 error | Callback URL mismatch — must be exactly `http://localhost:5173/callback` in the SPA app settings |
| Logged in but every API call returns 401 | `VITE_AUTH0_AUDIENCE` missing/wrong, or backend `Authentication:Authority` not set in User Secrets |
| Logged in but a page returns 403 | The test user's Auth0 role doesn't satisfy the endpoint policy — check the user's Roles tab and that the post-login Action is deployed **and attached to the flow** |
| Roles missing from the token | The Action isn't in the Login flow, or the claim namespace differs from `https://liansureprotect.local/roles` |
