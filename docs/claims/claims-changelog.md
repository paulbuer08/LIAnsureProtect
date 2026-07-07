# Claims Branch Changelog

> **⚠️ Historical archive (folded into the living docs at the Phase-3 merge).** The current-state
> Claims documentation is Encyclopedia Chapter 12 (docs/encyclopedia/12-flow-claims.md), Build
> History Era 7 (docs/build-history.md), and the root CHANGELOG/project-status. This file is the
> point-in-time branch record, kept for forensic depth only.

> Branch-local changelog for `feat/claims-context` (Phase 3 — the Claims bounded context).
> Same spirit as the root `CHANGELOG.md`, which this branch deliberately does not touch; CM8's
> final-merge checklist folds these entries into the living docs when the branch merges to main.

## Post-CM8 Hardening - Server-Authoritative Roles (`/me` Endpoint)

- Replaced the SPA's ID-token role parsing with a provider-neutral `GET /api/v1/me`
  (`CurrentUserController`, `[Authorize]`) that returns identity + roles from the same
  `ICurrentUser` the authorization policies use — so UI-shown roles and API-enforced roles can
  never drift. `RequireRole` now reads roles from `useCurrentUser` (TanStack Query, cached); it
  gained loading / lookup-error / **no-roles-assigned** / wrong-role states. Deleted
  `lib/userRoles.ts` — the SPA no longer parses any token.
- Obsoletes the "add roles claim to Auth0 **ID** tokens" action item (the SPA doesn't read the
  ID token anymore); the access-token roles claim (since M7) remains the only requirement. Fully
  provider-neutral for the M48 Cognito option. See `docs/claims/post-cm8-current-user-endpoint.md`.
- Tests: 3 new backend (`CurrentUserEndpointTests`) + `RequireRole` rewritten to 5 states;
  181 unit + 240 integration green, frontend 61 green, lint + build clean.

## Post-CM8 Audit - Split Queries, Reserve Release On Close, Queue Projection

- Full adversarial re-review of the branch (~17,300-line diff) — see
  `docs/claims/post-cm8-audit.md`. Fixed: `AsSplitQuery` on the three multi-collection claim
  include-graphs (cartesian-explosion prevention), the adjudication queue became a pure SQL
  projection (open-question count via subquery instead of materializing every information
  request), and `Close` now auto-releases any outstanding reserve with an audited change row
  (the reserve was previously frozen forever after a decision).
- Recorded (inherited/config findings, added to the final-merge checklist): tokenless download
  anchors (shared with evidence), Kestrel 30 MB default vs 50 MB upload governance (shared),
  orphaned blob on post-decision uploads, Auth0 ID-token roles-claim verification for
  `RequireRole`, shared `evidence-documents/` storage prefix.
- Tests: 2 new domain tests (reserve release + zero-reserve no-noise); 181 unit + 237
  integration green with every pre-existing test unchanged.

## Post-CM8 Fixes - Document Download Authentication

- Adopted the shared authenticated-download helper (`lib/documentDownload.ts`, landed on main
  via PR #52 together with the Kestrel 60 MB body-cap fix) in the claims pages: the owner claim
  detail and the adjuster workbench now download documents with a bearer-token fetch → blob
  save instead of bare links that 401ed; inline error surfaces added.
- Audit findings 1 and 2 from `post-cm8-audit.md` marked resolved; final-merge checklist items
  checked off. Remaining open findings: Auth0 ID-token roles-claim verification (tenant task),
  orphaned-blob janitor (deferred), storage prefix (won't fix).

## Claims Milestone 8 - Branch Consolidation Prep

- Added `docs/claims/final-merge-checklist.md`: the checkbox-level plan for the single
  consolidation PR that folds this branch's docs into the Tier-1 living documents when
  `feat/claims-context` merges to main after Phase 2 (encyclopedia Chapter 12, Build History
  Era 7, CHANGELOG, project-status, roadmap, user-roles, guides, READMEs), plus pre-merge
  verification and post-merge follow-ups.
- Executed and recorded the dry-run merge of `origin/main` into the parent: **clean**
  ("Already up to date" — main still at the branch point after eight milestone-start syncs).
- Recorded branch totals: 8 CI-green squash-merged PRs, ~200 new backend tests
  (179 unit + 237 integration green at close), 24 new frontend tests (59 green), 5 additive
  `claims` migrations, zero pushes to main, zero doc conflicts.

## Claims Milestone 7 - Frontend Claims Slice

- Added the `features/claims` vertical slice: typed API client (Idempotency-Key on file and
  accept/deny), TanStack Query hooks (adjudication mutations invalidate on `onSettled` so a
  409 refetches the truth — the M44.5 UX), and four pages.
- Claimant journey: `/claims/new` two-step wizard (bound-policy picker → incident form →
  confirmation), `/claims` list, `/claims/:id` detail (verdict, claimed-amount form, adjuster
  questions with inline answers, scan-gated document upload/download, timeline).
- Adjuster workbench `/claims/adjudication`: queue → working file with assign/release,
  financial summary cards, reserve form, information requests, accept (cap hint = limit net of
  retention), deny (category + narrative), close, notes, documents, reserve history, decision
  audit, timeline.
- New `RequireRole` route guard + `lib/userRoles.ts` (namespaced role claim); dashboard gained
  Claims + Claims-adjudication cards.
- Backend enabler: `GET /api/v1/claims/policy-options` via the new `ListOwnedBoundPoliciesAsync`
  port method.
- Tests: 24 new frontend (59 total green, lint + build clean) + 2 backend.
- Docs: `docs/claims/cm7-frontend-claims-slice-{design,learnings}.md`.

## Claims Milestone 6 - Notifications

- Added seven claim notification mappers (filed / assigned / information-requested /
  information-response / accepted / denied / closed) into the M40 registry — zero dispatcher
  changes; info requests are remediation-style (`actionRequired=true`); accept carries the
  settlement amount (invariant-culture).
- Added the **`claims-operations` team audience**: filings and claimant responses land in a
  shared team inbox with per-user read receipts (M34 machinery unchanged);
  `NotificationTeamAudiences` is now role-additive (ClaimsAdjuster → claims-operations,
  Admin → all three); `Notifications.Read` admits ClaimsAdjuster.
- Legacy Infrastructure gained the named transitional seam reference to `Modules.Claims.Domain`
  (mirror of the M37 Underwriting seam).
- Tests: 13 new (7 mapper, 5 audience-matrix, 1 projector). No migration needed.
- Docs: `docs/claims/cm6-notifications-{design,learnings}.md`.

## Claims Milestone 5 - Decision And Settlement

- Added the verdict endpoints: accept (settlement + reason + notes, **Idempotency-Key**),
  deny (reason category + narrative, **Idempotency-Key**), close — all on
  `/api/v1/claims/adjudication/{id}`.
- Charter guardrails domain-enforced + endpoint-tested: **no decision without assignment**
  (409), **no settlement over limit net of retention** (judged against the file-time snapshot;
  boundary tested both sides), **denial requires category + narrative** (400).
- Append-only `claims.claim_decisions` audit rows for every outcome, snapshotting claimed +
  reserve at decision time; `PaidAmount` written at settlement (local-sim posture).
- `ClaimAccepted`/`ClaimDenied`/`ClaimClosed` events into the module outbox (CM6 maps them);
  owner detail gained the verdict block (settlement / denial reason + narrative / timestamps).
- CM1's bare Accept/Deny/Close transitions absorbed into the rich decision methods.
- `AddClaimDecisions` migration. Tests: 27 new (15 domain, 11 endpoint, 1 migration).
- Docs: `docs/claims/cm5-decision-settlement-{design,learnings}.md`.

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
