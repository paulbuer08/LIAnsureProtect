# Claims Milestone 5 - Decision And Settlement — Learnings

> Companion to [the design doc](cm5-decision-settlement-design.md). Branch-local.

## What shipped

- The verdict endpoints on `/api/v1/claims/adjudication/{id}`: **accept** (settlement, reason,
  notes — **Idempotency-Key supported**), **deny** (reason category + narrative —
  **Idempotency-Key supported**), **close**.
- **All three charter guardrails, domain-enforced and endpoint-tested:**
  1. *No decision without assignment* — `EnsureAssignedDecider`: unassigned or another adjuster
     → 409 (proven at both unit and endpoint level, including a claim in UnderReview that was
     never assigned).
  2. *No settlement over the cap* — `settlement ≤ PolicyLimitAtFiling − PolicyRetentionAtFiling`,
     judged against the **file-time snapshot** (the reason CM1 snapshotted policy money);
     boundary tested at exactly-the-cap (legal) and one-cent-over (409).
  3. *Denial requires a reason* — category enum + mandatory narrative (400 on either missing).
- Append-only `claims.claim_decisions` audit rows for **every** outcome (Accepted / Denied /
  Closed) snapshotting claimed + reserve amounts at decision time.
- `ClaimAccepted` / `ClaimDenied` / `ClaimClosed` domain events into the module outbox
  (CM6 maps "decided" + "closed").
- Owner detail gained the verdict block (settlement amount, denial reason + narrative,
  decided/closed timestamps) — the claimant sees the outcome and the money, still never the
  reserve.

## Decisions and why

**Payment is recorded at settlement.** `Accept` sets `PaidAmount = SettlementAmount` — there is
no separate payment step because the platform's posture is local-simulation of externals
(binding acknowledgements, notifications publishes). A real payment integration would be a port
(`IClaimPaymentProviderClient`) + attempt-audit table, exactly the M19 shape — recorded as
future scope, not needed to prove the claims flow.

**Close writes an audit row too.** Closing is administrative, but an audit trail with silent
terminal transitions invites "who closed this?" questions — outcome `Closed` rows keep the
ledger complete, and the close event gives CM6 its "closed" notification for free.

**CM1's bare `Accept`/`Deny`/`Close` were absorbed** into the rich decision methods (third time
the pattern is applied: CM2 information requests, CM3 froze uploads, now decisions). A status
flip without a decision row behind it is now structurally impossible.

**Idempotency lives on accept/deny only.** The branch charter names "file claim, decision" as
the Idempotency-Key POSTs. Close is a no-payload administrative action whose retry is naturally
harmless (second close → 409, no state corruption), so the receipt machinery would be dead
weight there.

## Gotchas hit

- The idempotent replay test asserts **response-body equality** between first and replayed
  calls — this caught nothing this time, but it is the strongest cheap assertion for receipt
  replays (identical bytes, not just identical status).
- `Enum.TryParse` accepts numeric strings ("3" parses as a valid enum) — acceptable here because
  the parsed value is still a defined category via names used by the UI; the invalid-category
  test uses a non-numeric junk value, matching the evidence controller's precedent.

## Verification

- Full backend suite: **177 unit + 223 integration passed**, 4 skipped (opt-in), zero warnings.
- New coverage: 15 domain tests (guardrail matrix incl. cap boundary both sides, audit snapshots,
  events for all three outcomes, closed-file lockout), 11 endpoint tests (both-sides verdict
  visibility, cap 409/200 boundary pair, unassigned + wrong-adjuster 409s, narrative/category
  400s, deny→close lifecycle with outbox assertion, idempotent replay with byte-equal responses
  + single audit row, customer 403), 1 migration fact. Existing tests updated to the rich API.
- `AddClaimDecisions` migration applied to local Docker Postgres (additive).

## Intentionally not built yet

Notification mappers (CM6 — the events are queued and waiting), claim reopening (a decided/closed
file is frozen; reopening is a governance feature with its own audit needs), real payment
provider port (see above), UI (CM7).
