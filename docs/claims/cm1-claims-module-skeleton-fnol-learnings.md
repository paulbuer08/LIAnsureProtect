# Claims Milestone 1 - Claims Module Skeleton And FNOL — Learnings

> Companion to [the design doc](cm1-claims-module-skeleton-fnol-design.md). Branch-local
> (`feat/claims-context`); folded into the Tier-1 living docs at final merge (see
> `final-merge-checklist.md`, CM8).

## What shipped

- `src/Modules/Claims/{Domain,Application,Infrastructure}` — the fifth bounded-context module,
  owning the **`claims`** PostgreSQL schema (`ClaimsDbContext`, migrations history table inside
  the schema) with its own transactional outbox (`claims.outbox_messages`) and a registered
  `ClaimsOutboxSource` the existing dispatcher drains automatically.
- The `Claim` aggregate: full Filed → UnderReview → InformationRequested → Accepted/Denied →
  Closed state machine (guard methods; illegal transitions throw), append-only timeline,
  file-time policy snapshot, `Version` optimistic-concurrency token, `ClaimFiledDomainEvent`.
- FNOL: `POST /api/v1/claims` (Idempotency-Key supported) + owner-scoped `GET /api/v1/claims`
  and `GET /api/v1/claims/{id}` behind new `Claims.File` / `Claims.Read` policies
  (Customer/Broker/Admin).
- Cross-context read port `IClaimsPolicyContextReader` (Claims Application) implemented by
  `ClaimsPolicyContextReader` (legacy Infrastructure) — policy referenced by id only, no FK.
- Wiring: both hosts register `AddClaimsModule`; `claims-db` readiness check; CI + local script
  gained the fourth `dotnet ef database update --context ClaimsDbContext` step; architecture
  boundary tests extended (Claims rows + host reference lists).

## Decisions and why

**Policy facts are snapshotted at filing.** A claim is adjudicated against the policy *as it
was when the loss was reported*. Copying number/period/limit/retention onto the claim row (like
bind-time snapshots on `Policy`) means CM5's "settlement ≤ limit net of retention" guardrail
can never be silently changed by later policy-side edits, and the Claims module never needs a
second policy read after filing.

**The whole state machine landed in CM1, endpoints later.** Only `File` has an endpoint today,
but `StartReview`/`RequestInformation`/`RecordClaimantResponse`/`Accept`/`Deny`/`Close` are
implemented and unit-tested now. The lifecycle is the aggregate's constitution — designing it
once prevents CM2/CM5 from re-litigating transition rules. Transition *events* were deliberately
NOT added yet: every outbox event should ship in the milestone that gives it a consumer story
(CM6 wires notification mappers), otherwise unconsumed event types accumulate.

**`Version` token from day one.** CM2 copies the M44.5 assignment-claim pattern; retrofitting a
concurrency token onto a live table costs a migration and a risky backfill. Bumping it in
`Touch()` on every mutation (exactly like `QuoteReferralOperation`) is nearly free now.

**Ownership returns 404, not 403.** Filing against someone else's policy and filing against a
non-existent policy are indistinguishable to the caller — the same no-existence-leak convention
as submissions/quotes.

**Admin does not bypass ownership for filing.** `Claims.File` admits Admin (superuser by
design), but the handler still requires `policy.OwnerUserId == caller`. An admin filing on
behalf of a customer is a future feature with its own audit trail, not a silent bypass.

## Gotchas hit

- **SQLite in endpoint tests:** the integration suite swaps `ClaimsDbContext` (and
  `SubmissionDbContext` for policy seeding) to in-memory SQLite exactly like the Underwriting
  tests. `HasDefaultSchema("claims")` is a no-op on SQLite, which is fine — schema assertions
  live in the migration-script test (`CreateClaimsSchemaMigrationTests`) which generates the
  real Npgsql SQL without needing a database.
- **Seeding a bound policy** requires walking the real domain path
  (`Submission.CreateDraft → Submit → Quote.Generate → Accept → Policy.BindFromAcceptedQuote →
  Quote.MarkBound`) and clearing domain events so seeding doesn't pollute the legacy outbox.
  There is deliberately no test-only shortcut constructor.
- **The zero-warning gate bites on unused parameters:** `AddClaimsModule` keeps the uniform
  `(connectionString, profile)` module signature but has no profile-switched adapter yet;
  discarding it (`_ = profile;`) with a comment beats dropping the parameter and churning both
  hosts' composition roots again in CM3.

## Verification

- `dotnet test LIAnsureProtect.slnx` — 104 unit + 178 integration passed, 4 skipped (opt-in
  LocalStack/Redis), zero warnings.
- New coverage: 18 aggregate tests, 6 file-claim handler tests, 3 query handler tests,
  14 endpoint tests (roles, 404/409/400, idempotency replay, outbox row, owner scoping,
  timeline), 1 migration-script test, extended architecture tests.
- Local end-to-end: `claims` schema applied to the Docker Postgres via
  `scripts/update-database.ps1` (additive; safe beside the Phase 2 work in the main folder).

## Intentionally not built yet

Adjuster surface (CM2), documents (CM3), amounts (CM4 — the claim carries **no money fields**
yet by design), decisions with guardrails (CM5), notification mappers for `ClaimFiledDomainEvent`
(CM6 — the event is captured and dispatched today, but no consumer maps it, which the dispatcher
treats as a processed no-op), frontend (CM7).
