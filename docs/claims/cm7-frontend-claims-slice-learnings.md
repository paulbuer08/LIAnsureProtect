# Claims Milestone 7 - Frontend Claims Slice — Learnings

> Companion to [the design doc](cm7-frontend-claims-slice-design.md). Branch-local.

## What shipped

- The **`features/claims` vertical slice** (the M9 convention): `types.ts`, `api/claimsApi.ts`
  (all claimant + adjudication endpoints; file-claim and accept/deny send an
  **`Idempotency-Key`** via `crypto.randomUUID()`), `hooks/useClaims.ts` +
  `hooks/useClaimsAdjudication.ts` (TanStack Query with invalidation).
- **Claimant pages:** `/claims/new` (two-step wizard: pick a bound policy from the new
  policy-options endpoint → describe the incident → file, with a confirmation screen),
  `/claims` (list), `/claims/:claimId` (status + verdict, claimed-amount form, adjuster
  questions with inline answers, scan-gated document upload/download, timeline).
- **Adjuster workbench** `/claims/adjudication`: queue with assignment state → expandable
  working file (assign/release, claimed/reserve/paid summary cards, reserve form with mandatory
  reason, information-request form, accept form showing the **settlement cap** = limit net of
  retention, deny form with category + narrative, close button after a decision, work notes,
  documents, reserve history, decision audit, timeline).
- **Role guards:** new `RequireRole` component + `lib/userRoles.ts` reading the namespaced
  role claim (`https://liansureprotect.local/roles`) — `/claims*` requires
  Customer/Broker/Admin, `/claims/adjudication` requires ClaimsAdjuster/Admin. Guards are UX
  only; the API policies remain the enforcement point (stated in the component doc).
- Dashboard gained the Claims and Claims-adjudication cards.
- **Backend enabler:** `GET /api/v1/claims/policy-options` (owner's bound policies) through a
  new `ListOwnedBoundPoliciesAsync` on the existing policy-read port — the wizard's picker.

## Decisions and why

**The 409-refetch UX is wired at the hook layer.** Every adjudication mutation uses
`onSettled` (not `onSuccess`) to invalidate the queue + working file — so a losing adjuster's
409 automatically refetches the truth and the panel shows the real assignee (the M44.5 pattern's
front half). The workbench test proves the queue is refetched after a rejected assignment.

**Frontend role guards are convenience, not security.** `RequireRole` prevents confusing
403 screens, but every claims endpoint is still policy-guarded server-side. The role claim
constant lives in one place (`lib/userRoles.ts`) so a future Auth0 namespace change is a
one-line edit.

**Plain `useState` forms in the workbench.** The submissions wizard uses react-hook-form + zod
because client validation mirrors server validation there; the workbench forms are short,
adjuster-facing, and the domain's 400/409 messages are surfaced verbatim — RHF would add
ceremony without changing outcomes. The claim wizard keeps native `required` + date inputs for
the same reason.

**Generic test values only** (per the branch charter): test data uses `customer-1`,
`adjuster-1`, `CLM-CYB-...` sample numbers — no real names or PII.

## Gotchas hit

- The worktree had no `node_modules` (the repo root's install doesn't apply to a git worktree)
  — `npm ci` in the worktree first, or vitest resolves against the main repo's config and fails.
- jsdom's `crypto.randomUUID` exists in the test environment, so the idempotency-key header
  needs no test shim.

## Verification

- Frontend: `npm run lint` clean, `npm run test` **59 passed** (24 new: 3 ClaimsPage,
  3 NewClaimPage, 5 ClaimDetailPage, 10 workbench incl. the 409-refetch proof, 3 RequireRole),
  `npm run build` succeeds.
- Backend: 179 unit + 237 integration passed (2 new: policy-options handler + endpoint).

## Intentionally not built yet

Notification deep links from claim notifications into claim pages (the notifications page
already lists the new entries), optimistic updates (refetch-on-settled is the simpler correct
default), pagination (queues are small; the API has no paging yet).
