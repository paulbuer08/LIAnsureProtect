# Claims Branch Changelog

> Branch-local changelog for `feat/claims-context` (Phase 3 — the Claims bounded context).
> Same spirit as the root `CHANGELOG.md`, which this branch deliberately does not touch; CM8's
> final-merge checklist folds these entries into the living docs when the branch merges to main.

## Claims Milestone 1 - Claims Module Skeleton And FNOL

- Added the Claims bounded-context module (`src/Modules/Claims/{Domain,Application,Infrastructure}`)
  owning the new `claims` PostgreSQL schema via `ClaimsDbContext` (+ `CreateClaimsSchema`
  migration; history table in-schema) and its own transactional outbox + `ClaimsOutboxSource`.
- Added the `Claim` aggregate: domain-enforced lifecycle (Filed → UnderReview →
  InformationRequested → Accepted/Denied → Closed), append-only timeline entries, file-time
  policy snapshot (number/period/limit/retention), optimistic-concurrency `Version` token, and
  `ClaimFiledDomainEvent` into the module outbox.
- Added FNOL endpoints: `POST /api/v1/claims` (files a claim against an owned **bound** policy,
  validated through the new read-only `IClaimsPolicyContextReader` port implemented legacy-side;
  Idempotency-Key supported) and owner-scoped `GET /api/v1/claims` + `GET /api/v1/claims/{id}`.
- Added `Claims.File` and `Claims.Read` authorization policies (Customer/Broker/Admin).
- Wired the module into both hosts, the `claims-db` readiness check, CI and
  `scripts/update-database.ps1` migration steps, and the architecture boundary tests.
- Tests: 42 new (18 aggregate, 9 application, 14 endpoint incl. idempotency replay + outbox
  assertion, 1 migration script) — full backend suite green under the zero-warning gate.
- Docs: `docs/claims/cm1-claims-module-skeleton-fnol-{design,learnings}.md`.
