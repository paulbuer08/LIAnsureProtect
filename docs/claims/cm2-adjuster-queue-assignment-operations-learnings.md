# Claims Milestone 2 - Adjuster Queue, Assignment And Operations — Learnings

> Companion to [the design doc](cm2-adjuster-queue-assignment-operations-design.md). Branch-local.

## What shipped

- **ClaimsAdjuster went live** — the role reserved since M6 got its first endpoints:
  `Claims.Adjudicate` (ClaimsAdjuster + Admin) guards the new
  `/api/v1/claims/adjudication` surface (queue, detail, assign/release, notes, information
  requests); `Claims.Respond` (Customer/Broker/Admin) guards the claimant's answer endpoint.
- **Assignment is the M44.5 guarded claim, verbatim semantics:** domain rejects a second
  adjuster, same-adjuster re-clicks are idempotent, release is the explicit hand-over, and the
  CM1 `Version` token catches the true race (proven by `ClaimAssignmentConcurrencyTests` — two
  contexts, second save throws `DbUpdateConcurrencyException` → repository translates → 409).
- First assignment on a `Filed` claim **starts the review** (Filed → UnderReview) — one action,
  two timeline entries.
- **Information request loop:** adjuster asks (claim → InformationRequested, event raised) →
  claimant sees the question on their own detail and answers (request marked answered, claim →
  UnderReview, event raised). Work notes are append-only with timeline entries.
- New events into the module outbox: `ClaimAssignedDomainEvent`,
  `ClaimInformationRequestedDomainEvent`, `ClaimantInformationResponseDomainEvent`
  (consumers arrive in CM6; the response event carries `AssignedAdjusterUserId` so CM6 can
  notify the working adjuster personally).

## Decisions and why

**No separate "operation" aggregate.** Underwriting needed `QuoteReferralOperation` because the
quote lives in another context and reaches the workbench by projection. The claim already *is*
this module's aggregate — filing and adjudication are one bounded context — so assignment,
notes, and information requests live directly on `Claim`. No projector, no dedupe table, no
eventual-consistency window: an adjuster action is a synchronous command on the module's own
aggregate (exactly the async-conventions rule "synchronous in the core, events at the seams").

**CM1's bare transition helpers were absorbed.** `RequestInformation(user, at)` and
`RecordClaimantResponse(user, at)` (transition-only) would have let code flip statuses without
creating/answering an actual information request — an invariant hole. The rich methods
(`RequestInformation(user, title, message, at)` → entity + transition;
`RespondToInformationRequest(id, user, text, at)` → answer + transition) are now the only doors.
CM1's domain tests were updated in the same commit — refactoring tests alongside a deliberate
API change is normal TDD, *replacing* their assertions is not (assertions got stronger, not
weaker).

**Status is a coarse flag, not a per-request ledger.** The first claimant answer flips
InformationRequested → UnderReview even if other requests are still open (they stay visible and
answerable). Recorded as a simplification; if adjusters need "all requests answered" semantics,
that is a CM5-adjacent enhancement with its own test.

**The queue is not cached.** M44.5 cached the referral queue only after it was measurably the
hottest read with three fan-out readers. The claims queue starts uncached; `ICacheableRequest`
is a one-marker adoption later if it earns it.

**Version-bump assertions loosened deliberately.** CM1 asserted exact `+1` per mutation; rich
actions now touch more than once (assign = claim + transition). The test now asserts
monotonic increase — the *contract* is "every mutation changes the token", not "by exactly one".

## Gotchas hit

- `Created(Request.Path, result)` needs the note/request id appended for a strictly-correct
  Location header; kept simple (`Request.Path`) matching the evidence-request controller's style.
- SQLite + owned collections: `Include` chains on three child collections work fine, but the
  cartesian-explosion warning does not fire at this size; revisit with `AsSplitQuery` if claims
  accumulate hundreds of children (noted for CM8's checklist, not needed now).

## Verification

- Full backend suite: **123 unit + 193 integration passed**, 4 skipped (opt-in), zero warnings.
- New coverage: 15 domain tests (assignment claim semantics, info-request flow, guards),
  5 handler tests, 1 persistence concurrency proof, 10 endpoint tests (roles, 409 + survivor
  assertion, release/reassign, notes, full info round trip, outbox event, ownership 404),
  2 migration-script facts.
- `AddClaimOperations` migration applied to local Docker Postgres (additive).

## Intentionally not built yet

Documents on information-request responses (CM3 pairs uploads with the scan gate), triage
(priority/SLA — deliberately omitted: claims severity work arrives with reserves in CM4 where
severity has money meaning), decision guardrails (CM5), notification mappers (CM6), UI (CM7).
