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
| CM2 — Adjuster queue + assignment + operations | ⬜ next |
| CM3 — Claim documents | ⬜ |
| CM4 — Reserves & financials | ⬜ |
| CM5 — Decision & settlement | ⬜ |
| CM6 — Notifications | ⬜ |
| CM7 — Frontend claims slice | ⬜ |
| CM8 — Branch consolidation prep | ⬜ |

## Current state (after CM1)

- Module: `src/Modules/Claims` (Domain/Application/Infrastructure), `claims` schema, module
  outbox, `ClaimsOutboxSource` registered in both hosts.
- API: `POST /api/v1/claims`, `GET /api/v1/claims`, `GET /api/v1/claims/{id}`
  (`Claims.File` / `Claims.Read`, Customer/Broker/Admin).
- Cross-context: `IClaimsPolicyContextReader` (Claims) ← `ClaimsPolicyContextReader`
  (legacy Infrastructure), id-only policy reference + file-time snapshot.
- Verification: full backend suite green (zero-warning gate); `claims` schema migration applied
  in CI and locally.

## Decisions to remember

- Policy facts snapshot at filing (CM5 guardrails judge against these, never live policy rows).
- Claim state machine fully implemented + tested in CM1; endpoints arrive with their milestones.
- Transition domain events deferred to the milestone that gives them a consumer (CM6).
- `Version` concurrency token present from CM1 for CM2's assignment claim (M44.5 pattern).
- Ownership failures return 404 (no existence leak); Admin does not bypass filing ownership.
