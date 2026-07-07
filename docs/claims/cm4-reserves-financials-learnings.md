# Claims Milestone 4 - Reserves And Financials — Learnings

> Companion to [the design doc](cm4-reserves-financials-design.md). Branch-local.

## What shipped

- The claim's money picture: `ClaimedAmount` (nullable — the claimant's declaration),
  `ReserveAmount` (the insurer's estimate), `PaidAmount` (zero until CM5's settlement writes it).
- Claimant endpoint `POST /api/v1/claims/{id}/claimed-amount` (`Claims.Respond`, owner-scoped).
- Adjuster endpoint `POST /api/v1/claims/adjudication/{id}/reserve` — **assigned adjuster only**,
  mandatory reason, every change appends a `ClaimReserveChange` audit row
  (`claims.claim_reserve_changes`: old amount → new amount, reason, who, when).
- Both detail reads gained financials; the adjudication detail also returns the reserve history.

## Decisions and why

**The reserve is confidential.** The owner detail exposes `claimedAmount` and `paidAmount` but
**never** `reserveAmount` or the history — a claimant who can see the insurer's internal estimate
gains a negotiation ceiling. This is a real insurance-industry rule, enforced by an endpoint test
asserting the fields are absent from the owner payload.

**Only the assigned adjuster moves money.** Notes and information requests are permissive
(CM2), but a reserve is a financial commitment, so the domain requires
`AssignedAdjusterUserId == actor` — stricter than the referral-operations precedent, on purpose.
Unassigned or wrong-adjuster attempts are 409s.

**Claimed amount is uncapped.** A claimant may demand more than the policy limit; the CM5
settlement guardrail caps what can be *paid* (limit net of retention), not what can be asked.
Capping the demand would silently rewrite the claimant's position — bad audit hygiene.

**Same-amount reserve changes are rejected.** An audit log where "changed 150,000 → 150,000"
rows can accumulate is noise that erodes trust in the history; a no-op is a caller mistake.

**No domain events for financial changes.** CM6's mapper list (filed / assigned /
info-requested / decided / closed) doesn't consume them; the append-only history + timeline are
the audit trail. If a "large reserve change" team alert is ever wanted, the event can be added
with its mapper in one milestone.

**Timeline money is invariant-culture** (`0.00`, `FormatMoney`) — the post-M44 audit found a
culture-sensitive formatting bug once already; an audit trail must not read differently
depending on the server's locale.

## Verification

- Full backend suite: **162 unit + 211 integration passed**, 4 skipped (opt-in), zero warnings.
- New coverage: 16 domain tests (claimed-amount rules incl. over-limit-allowed and post-decision
  freeze; reserve assignment guard both directions, negative/same-amount/missing-reason
  rejections, release-to-zero, ordered history, version bumps), 4 handler tests, 8 endpoint
  tests (owner flow + confidentiality assertion, history round trip, 409/400/403 matrix),
  1 migration fact.
- `AddClaimFinancials` migration applied to local Docker Postgres (additive).

## Intentionally not built yet

Settlement/payment writes (CM5 records `PaidAmount` on Accept), payment schedules/partial
payments (post-branch scope if ever), reserve-change notifications (would need a new mapper —
see the no-events decision above).
