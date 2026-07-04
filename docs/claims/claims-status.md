# Claims Branch Status

> Branch-local status for `feat/claims-context` (same spirit as `docs/project-status.md`, which
> this branch does not touch). Updated at every Claims milestone close.

- **Branch model:** child branches `feat/claims/cm<N>-<slug>` → PR into `feat/claims-context`
  (never main) → CI green → squash-merge. `origin/main` is merged into the parent at the start
  of every milestone.
- **Docs policy:** all Claims docs live under `docs/claims/`; Tier-1 living docs are untouched
  until the final merge (CM8 checklist).

## Milestone progress

| Milestone | Status |
|---|---|
| CM1 — Claims module skeleton + FNOL | ✅ merged |
| CM2 — Adjuster queue + assignment + operations | ✅ merged |
| CM3 — Claim documents | ✅ merged |
| CM4 — Reserves & financials | ✅ merged |
| CM5 — Decision & settlement | ✅ merged |
| CM6 — Notifications | ✅ merged |
| CM7 — Frontend claims slice | ✅ merged |
| CM8 — Branch consolidation prep | ⬜ next |

## Current state (after CM7)

- Frontend: `features/claims` slice — claimant wizard/list/detail + adjuster workbench at
  `/claims/adjudication`; `RequireRole` guards; dashboard cards; policy-options endpoint.
- The Claims context is now feature-complete end-to-end (API + events + inboxes + UI); CM8
  prepares the final merge back to main.

## State after CM6

- Notifications: all lifecycle events mapped to personal + team inboxes; new
  `claims-operations` team audience (ClaimsAdjuster/Admin); remediation-style info-request
  messages; `Notifications.Read` includes ClaimsAdjuster.

## State after CM5

- Decisions: accept/deny/close with all three charter guardrails; append-only
  `claim_decisions` audit; Idempotency-Key on accept/deny; verdict block on the owner detail;
  ClaimAccepted/Denied/Closed events queued for CM6; migration `AddClaimDecisions`.
- Full lifecycle now works end-to-end via API: file → assign → info round trip → documents →
  reserve → decide → close.

## State after CM4

- Financials: claimed/reserve/paid on the aggregate; owner declares the claimed amount;
  the assigned adjuster moves the reserve with append-only history; reserve is confidential
  to the adjudication side; migration `AddClaimFinancials`.

## State after CM3

- Documents: scan-gated uploads (`Claims.Respond`), clean-only downloads on both surfaces,
  module-owned `IClaimDocumentScanner` (local deterministic) + shared Platform storage port;
  `ClaimDocumentUploadedDomainEvent` in the outbox; migration `AddClaimDocuments`.

## State after CM2

- Module: `src/Modules/Claims` (Domain/Application/Infrastructure), `claims` schema, module
  outbox, `ClaimsOutboxSource` registered in both hosts.
- Claimant API: `POST /api/v1/claims`, owner-scoped list/detail (now incl. information
  requests), `POST /{id}/information-requests/{rid}/respond`
  (`Claims.File` / `Claims.Read` / `Claims.Respond`, Customer/Broker/Admin).
- Adjuster API: `/api/v1/claims/adjudication` — queue, detail, assign/release (M44.5 guarded
  claim + Version token → 409), notes, information requests (`Claims.Adjudicate`,
  ClaimsAdjuster/Admin — the reserved role is now live).
- Events in the module outbox: ClaimFiled, ClaimAssigned, ClaimInformationRequested,
  ClaimantInformationResponse (mappers arrive in CM6).
- Cross-context: `IClaimsPolicyContextReader` (Claims) ← `ClaimsPolicyContextReader`
  (legacy Infrastructure), id-only policy reference + file-time snapshot.
- Verification: full backend suite green (zero-warning gate); migrations
  `CreateClaimsSchema` + `AddClaimOperations` applied in CI and locally.

## Decisions to remember

- Policy facts snapshot at filing (CM5 guardrails judge against these, never live policy rows).
- Claim state machine fully implemented + tested in CM1; endpoints arrive with their milestones.
- Transition domain events deferred to the milestone that gives them a consumer (CM6).
- `Version` concurrency token present from CM1 for CM2's assignment claim (M44.5 pattern).
- Ownership failures return 404 (no existence leak); Admin does not bypass filing ownership.
