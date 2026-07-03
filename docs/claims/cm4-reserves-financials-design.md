# Claims Milestone 4 - Reserves And Financials — Design

> Branch-local doc (`docs/claims/` policy — see `claims-status.md`).

## What this milestone builds

The claim gets its **money picture**: the claimant declares what they are claiming
(**claimed amount**), the assigned adjuster sets and adjusts the **reserve** (the insurer's
best-estimate liability, with a mandatory reason and an **append-only reserve history**), and the
aggregate tracks **claimed vs reserve vs paid** — with `paid` staying zero until CM5's settlement
writes it.

> **Analogy:** the reserve is the money the insurer moves into an envelope marked with this
> claim's number the moment it looks real — not paid out, but no longer counted as free. Every
> time an adjuster changes the envelope's contents they must write on the envelope why
> (append-only history) — that log is what auditors and reinsurers read.

## Scope

In: `ClaimedAmount`/`ReserveAmount`/`PaidAmount` on the aggregate, claimant claimed-amount
endpoint, adjuster reserve endpoint (assignment-required), `claims.claim_reserve_changes`
append-only audit table, financial summary on both detail reads, `AddClaimFinancials` migration.

Out: settlement/payment writes (`PaidAmount` is written by CM5's Accept), notifications (CM6),
UI (CM7). **No domain events for financial changes** — CM6's mapper list (filed / assigned /
info-requested / decided / closed) does not consume them; the append-only history + timeline are
the audit trail. Recorded so CM6 does not hunt for missing events.

## Domain

On `Claim`:

- `ClaimedAmount` (decimal?, null until declared) — `SetClaimedAmount(amount, byUser, at)`:
  owner action, open claims only, amount > 0, capped at nothing (a claimant may claim above the
  limit; the *settlement* is what CM5 caps), timeline `ClaimedAmountUpdated`.
- `ReserveAmount` (decimal, default 0) + `SetReserve(amount, reason, byAdjusterUserId, at)`:
  **only the assigned adjuster** (a reserve is a financial commitment — the file's handler owns
  it; stronger than notes deliberately), open claims only, amount ≥ 0 (releasing a reserve to 0
  is legal and auditable), reason mandatory, appends a `ClaimReserveChange` row (old amount, new
  amount, reason, who, when) + timeline `ReserveChanged`. Setting the same amount is rejected
  (a no-op reserve change with a reason is noise in an audit log).
- `PaidAmount` (decimal, default 0) — no public setter this milestone; CM5's settlement records it.
- `ClaimReserveChange` child entity (`claims.claim_reserve_changes`, append-only).

## API surface

| Endpoint | Policy | Effect |
|---|---|---|
| `POST /api/v1/claims/{id}/claimed-amount` `{ amount }` | `Claims.Respond` (owner) | declare/update the claimed amount |
| `POST /api/v1/claims/adjudication/{id}/reserve` `{ amount, reason }` | `Claims.Adjudicate` + must be the assigned adjuster | set/adjust the reserve; 409 when unassigned or another adjuster |
| Both detail reads | existing | gain a `financials` block: claimedAmount, reserveAmount, paidAmount, policyLimitAtFiling, policyRetentionAtFiling; adjudication detail also returns the reserve history |

## Testing plan (TDD)

1. **Domain:** claimed amount (set/update, non-positive rejected, closed claim rejected);
   reserve (assigned-adjuster-only incl. unassigned and wrong-adjuster rejections, negative
   rejected, same-amount rejected, history appended with old→new snapshot, release-to-zero legal,
   closed claim rejected); paid stays zero; version bumps.
2. **Application:** handlers (owner scoping for claimed amount; reserve handler passes acting
   user; unknown claim → null).
3. **Endpoints:** claimed-amount owner flow + foreign-owner 404; reserve happy path + history in
   adjudication detail + 409s (unassigned, wrong adjuster) + financials block on both details;
   migration facts for columns + history table.
