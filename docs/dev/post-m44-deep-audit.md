# Post-M44 Deep Audit — Warnings, Hidden Bugs, Best Practices, Capability Coverage

Date: 2026-07-03. Scope: the entire solution (all backend projects, all tests, the React frontend),
requested as a final solidification pass before cache adoption and Phase 2.

## 1. Warning sweep — result: zero warnings, now permanently enforced

**Starting point.** The default build was already clean (0 warnings). The editor screenshot that
prompted this audit (`csharpsquid:S107`, 22-parameter constructor) is a **SonarLint IDE-side rule**,
not a build warning — but it pointed at a real design smell, fixed below.

**Bar raised.** The solution now builds with:

- `Directory.Build.props`: `AnalysisLevel = latest-recommended` (stricter .NET analyzer set) and
  **`TreatWarningsAsErrors = true`** — any future warning fails the build, locally and in CI.
- Result after fixes: **0 errors, 0 warnings** across every project; all 218 tests green;
  frontend ESLint/build/tests clean.

**What the stricter level surfaced, and what was done:**

| Rule | Count | Verdict | Action |
|---|---|---|---|
| CA2208 (wrong `paramName` in `ArgumentException`) | 2 | **Real bug** | Fixed in `EvidenceDocumentCommands` (`nameof(request.X)` → `nameof(request)`) |
| CA1305 (culture-sensitive `int.ToString()`) | 1 | **Real bug** (theoretical locale digit shaping in `Retry-After`) | `CultureInfo.InvariantCulture` |
| CA1848/CA1873 (logging performance) | 11 | Worth fixing — outbox/worker are hot loops | Converted `OutboxDispatcher`, `Worker`, and API startup to **source-generated `[LoggerMessage]`** (`OutboxDispatcherLog`, `WorkerLog`, `ApiStartupLog`) |
| CA1859 (interface where concrete type is faster) | 20 | Fixed in production + test helpers | Private members now use concrete `List<>`/`Dictionary<>`/arrays |
| CA1822 (can be static) | 4 | Fixed | Test helpers marked static |
| CA1861 (constant array arguments) | 114 | **Unavoidable — do not touch** | All in **EF Core scaffolded migrations** (generated, append-only history). Excluded via `.editorconfig` for `Migrations/*.cs` only |
| CA1707 (underscores in identifiers) | 356 | **Intentional — do not touch** | All are test method names using the standard `Does_X_When_Y` convention. Excluded via `.editorconfig` for `tests/**` only |

The two exclusions are the "unavoidable warnings driven by other factors" category: generated code
and a deliberate, industry-standard test-naming convention. Everything else is fixed, not silenced.

## 2. The S107 constructor smell — refactored across the whole domain

`Quote` had a private **22-parameter** constructor; nine more entities shared the pattern
(`AiUnderwritingReview` 21, `QuoteRatingProviderAttempt` 18, `Policy` 17,
`QuoteEvidenceRequestReview` 13, `QuoteUnderwritingReview` 11, `QuoteEvidenceDocument` 11,
`QuoteEvidenceRequest` 10, `NotificationInboxEntry` 10, `TeamNotificationEntry` 9).

**Fix applied to all ten:** the parameter-heavy private constructor is deleted; the parameterless
private constructor (which EF Core already used for materialization) is now the only one, and each
static factory assigns state through the private property setters. Behavior is identical — every
factory still validates before construction, and all domain tests pass unchanged. This kills the
smell honestly (no suppression) and stops it regrowing as aggregates gain fields.

## 3. Hidden-bug hunt — clean

Pattern probes across all production code found **zero** instances of: `DateTime.Now` (UTC is used
everywhere), sync-over-async (`.Result`/`.Wait()`/`GetAwaiter().GetResult()`), `async void`, empty
catch blocks, TODO/FIXME/HACK markers, `CancellationToken.None`, `Task.Run`, `new Random`, raw SQL
(`FromSqlRaw`/`ExecuteSqlRaw`), or secrets in committed appsettings. Frontend: no console logging,
no browser-storage token persistence, no untyped `any` usage, no raw-HTML injection APIs.

**Known, deliberate residual risks (tracked, not hidden):**

1. **Referral assign-to-me has no optimistic concurrency** — two underwriters clicking
   simultaneously could both "win" locally. Fix is specced as the next milestone
   (referral-queue caching + concurrency; see the roadmap).
2. **Orphaned stored files on partial upload failure** — if the process dies between storing bytes
   and committing metadata, a file can remain in storage without a DB row. Harmless (unreachable,
   private), standard for this design; a cleanup sweep belongs with the S3 lifecycle rules (M46).
3. **In-process rate limiting is per-instance** — a shared Redis-backed limiter belongs with
   horizontal scale-out (EKS, Phase 2).

## 4. Capability & role coverage vs. a realistic specialty-insurance app

| Role | Status today |
|---|---|
| **Customer / Broker** | Fully wired: submissions (ownership-scoped), quotes, acceptance attestation, binding, evidence responses + document upload, notifications |
| **Underwriter** | Fully wired: referral queue + SLA/triage/notes/tasks/timeline, evidence request/review, advisory AI, approve/decline/adjust, team inbox |
| **Admin** | Wired as **superuser** — included in every policy. A dedicated admin console (user/product/rules management) is future scope; the `System.Admin` policy exists and is reserved |
| **ClaimsAdjuster** | **Role constant reserved; no behavior** — correct by design: the `Claims` bounded context is in the roadmap's target contexts and has not been built. FNOL/claims workflows are a future milestone (post-Phase-2), not a gap in the current scope |

Business-realism verdict: the platform covers the **pre-bind lifecycle** of a specialty cyber MGA
(intake → rating → referral underwriting → evidence → decision → acceptance → bind) with real
production patterns (outbox, idempotency, ownership AuthZ, scan-gated documents, audit rows).
The **post-bind** world (claims/FNOL, billing, renewals, endorsements, admin console) is
deliberately future scope, documented in the roadmap's bounded-context target list.

## 5. Architecture/coupling review — no new findings

The module boundaries hold (architecture tests ratchet them), cross-context reads go through
ports, no cross-schema FKs, adapters are profile-selected with fail-fast misconfiguration, and
outbound HTTP is typed-client + resilience. The 2026-07-02 solidification audit's findings remain
accurate; nothing regressed in M42–M44.

## 6. New documentation delivered with this audit

- [Running The App — complete current guide](../guides/running-the-app.md) (living)
- [Manual UI Testing Guide — every role, generic personas](../guides/manual-testing-guide.md) (living)
- `src/LIAnsureProtect.Web/.env.example` (generic frontend config template)
- The old `docs/dev/run-the-app.md` is banner-marked as a Milestone-9 historical snapshot.
