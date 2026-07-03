# Claims Milestone 5 - Decision And Settlement — Design

> Branch-local doc (`docs/claims/` policy — see `claims-status.md`).

## What this milestone builds

The claim lifecycle reaches its verdict: the **assigned adjuster** accepts the claim with a
**settlement amount capped at the policy limit net of retention**, or denies it with a **reason
category + narrative**, then **closes** the file — every outcome writing an **append-only
decision audit row** and a **domain event** into the module outbox.

> **Analogy:** the stamp on the folder. Only the adjuster whose name is on the folder may stamp
> it; the accept stamp cannot promise more money than the policy envelope minus the deductible;
> the deny stamp is invalid without a written reason; and every stamp leaves a carbon copy in a
> bound ledger nobody can tear pages out of.

## Guardrails (the milestone's contract, each one tested)

1. **No decision without assignment** — Accept/Deny/Close require the acting adjuster to be the
   assigned adjuster (unassigned claim or another adjuster → 409).
2. **No settlement over the cap** — `settlement ≤ PolicyLimitAtFiling − PolicyRetentionAtFiling`
   (judged against the **file-time snapshot**, exactly why CM1 snapshotted it); over-cap → 409;
   non-positive → 400.
3. **Denial requires a reason** — category (NotCovered, PolicyExclusion, OutsidePolicyPeriod,
   InsufficientEvidence, MisrepresentationFraud, Other) **and** a narrative; missing → 400.

## Domain

- CM1's bare `Accept(user, at)` / `Deny(user, at)` / `Close(user, at)` transition methods are
  **absorbed into rich decision methods** (same refactor discipline as CM2's information
  requests — a status flip without a real decision behind it becomes impossible):
  - `Accept(settlementAmount, reason, notes?, adjusterUserId, decidedAtUtc)` — UnderReview only;
    cap + positivity checks; sets `SettlementAmount` **and** `PaidAmount = settlement` (payment
    is simulated at settlement, consistent with the platform's local-sim posture); audit row;
    `ClaimAcceptedDomainEvent`.
  - `Deny(denialReason, narrative, adjusterUserId, decidedAtUtc)` — UnderReview only; audit row;
    `ClaimDeniedDomainEvent`.
  - `Close(adjusterUserId, closedAtUtc)` — Accepted/Denied only; audit row (outcome `Closed`);
    `ClaimClosedDomainEvent`.
- `ClaimDecision` append-only child (`claims.claim_decisions`): outcome, settlement amount,
  denial reason, reason/narrative, notes, **claimed + reserve amounts snapshotted at decision
  time**, decided-by, decided-at.
- Convenience current-state fields on `Claim`: `SettlementAmount?`, `DenialReason?`,
  `DenialNarrative?`, `DecidedByUserId?`, `DecidedAtUtc?`, `ClosedAtUtc?`.

## API surface (adjudication controller)

| Endpoint | Effect |
|---|---|
| `POST /{claimId}/accept` `{ settlementAmount, reason, notes? }` | **Idempotency-Key supported** (the "decision" POST named by the branch charter); 200 + decision result |
| `POST /{claimId}/deny` `{ reasonCategory, narrative }` | **Idempotency-Key supported**; 200 + decision result |
| `POST /{claimId}/close` | plain POST (administrative step after a decision) |
| Adjudication detail | gains the `decisions` audit array |
| Owner detail | gains a `decision` block (outcome, settlement amount, denial reason + narrative, decided-at) — the claimant sees the verdict and the money, never the reserve |

## Testing plan (TDD)

1. **Domain:** happy accept (settlement + paid set, audit row with claimed/reserve snapshot,
   event), cap boundary (exactly at cap legal, one cent over rejected), non-positive rejected,
   accept/deny require UnderReview + assigned actor (both directions), denial category+narrative
   required, close only after decision + by assigned actor, closed file rejects everything,
   audit rows append in order, events for all three outcomes.
2. **Application:** handlers pass acting user; unknown claim → null.
3. **Endpoints:** accept round trip (owner sees decision block incl. settlement; adjudication
   detail shows audit), deny round trip, close, guardrail matrix (409 unassigned / wrong
   adjuster / over cap / not-under-review; 400 non-positive / missing narrative / bad category),
   idempotency replay on accept (same key → same response, one decision row), events in outbox,
   migration facts.
