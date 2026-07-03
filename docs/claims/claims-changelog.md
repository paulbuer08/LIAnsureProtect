# Claims Branch Changelog

> Branch-local changelog for `feat/claims-context` (Phase 3 — the Claims bounded context).
> Same spirit as the root `CHANGELOG.md`, which this branch deliberately does not touch; CM8's
> final-merge checklist folds these entries into the living docs when the branch merges to main.

## Claims Milestone 4 - Reserves And Financials

- Added the claim money picture: `ClaimedAmount` (claimant's declaration, uncapped — CM5 caps
  the settlement), `ReserveAmount`, `PaidAmount` (written by CM5).
- Added `POST /api/v1/claims/{id}/claimed-amount` (owner-scoped) and
  `POST /api/v1/claims/adjudication/{id}/reserve` (**assigned adjuster only**, mandatory
  reason, append-only `claims.claim_reserve_changes` audit rows).
- Financials on both detail reads; reserve amount + history are **confidential to the
  adjudication side** (never serialized to the claimant — endpoint-tested).
- Invariant-culture money in timeline entries. No domain events for financial changes
  (recorded decision — the append-only history is the audit trail; CM6 maps lifecycle events).
- `AddClaimFinancials` migration. Tests: 29 new (16 domain, 4 handler, 8 endpoint, 1 migration).
- Docs: `docs/claims/cm4-reserves-financials-{design,learnings}.md`.

## Claims Milestone 3 - Claim Documents

- Added scan-gated supporting documents: `ClaimDocument` children (`claims.claim_documents`)
  with kind, private storage key, scan status/scanner metadata/SHA-256; fail-closed —
  `IsDownloadAvailable` only when scanned Clean.
- Added `POST /api/v1/claims/{id}/documents` (multipart, owner-scoped, `Claims.Respond`):
  store via the shared Platform `IDocumentStorageService` → quarantine-scan via the new
  module-owned `IClaimDocumentScanner` (local deterministic adapter, same test markers as
  evidence) → persist; per-file scan outcomes returned; `ClaimDocumentUploadedDomainEvent`
  per file into the module outbox.
- Added clean-only downloads on both surfaces (owner + adjudication routes; Rejected/Failed →
  409 for every role); replacement uploads append and rejected originals stay for audit;
  documents listed on both detail endpoints; uploads frozen once a claim is decided.
- Upload governance identical to evidence documents (5 files / 10 MB each / 50 MB total /
  content-type allow-list / no path info in names).
- `AddClaimDocuments` migration. Tests: 29 new (10 domain, 10 handler, 8 endpoint, 1 migration).
- Docs: `docs/claims/cm3-claim-documents-{design,learnings}.md`.

## Claims Milestone 2 - Adjuster Queue, Assignment And Operations

- Activated the reserved **ClaimsAdjuster** role: new `Claims.Adjudicate` (ClaimsAdjuster/Admin)
  and `Claims.Respond` (Customer/Broker/Admin) policies.
- Added the adjudication surface `/api/v1/claims/adjudication`: queue (open claims), full working
  detail (notes + information requests + timeline), assign-to-me / release-assignment with the
  **M44.5 guarded-claim + optimistic-concurrency pattern** (domain rejects a second adjuster;
  true races fail on the `Version` token → 409 → refetch), append-only work notes, and
  information requests.
- Added the claimant answer flow: `POST /api/v1/claims/{id}/information-requests/{rid}/respond`
  (owner-scoped); the owner claim detail now includes information requests; first assignment
  starts the review (Filed → UnderReview); claimant answers return the claim to UnderReview.
- New domain events into the module outbox: `ClaimAssignedDomainEvent`,
  `ClaimInformationRequestedDomainEvent`, `ClaimantInformationResponseDomainEvent`.
- `AddClaimOperations` migration: `assigned_adjuster_user_id` (+ index),
  `claims.claim_work_notes`, `claims.claim_information_requests`.
- Tests: 33 new (15 domain, 5 handler, 1 concurrency proof, 10 endpoint, 2 migration facts).
- Docs: `docs/claims/cm2-adjuster-queue-assignment-operations-{design,learnings}.md`.

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
