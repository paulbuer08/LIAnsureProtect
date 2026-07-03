# Claims Milestone 6 - Notifications — Learnings

> Companion to [the design doc](cm6-notifications-design.md). Branch-local.

## What shipped

- Seven registered `IOutboxMessageMapper<NotificationMessage>` classes turn the claim events
  (queued since CM1) into inbox entries: personal (customer-or-broker) for assignment,
  information requests (**remediation-style, `actionRequired=true`**), accept (with the
  settlement amount), deny (with the reason category), and close; the **new `claims-operations`
  team audience** for new filings and claimant responses.
- `NotificationTeamAudiences.ForRoles` became role-additive: Underwriter → underwriting+binding
  (unchanged), **ClaimsAdjuster → claims-operations**, Admin → all three, combined roles union.
- `NotificationInboxProjector` routes `claims-operations` to the shared team-entry path
  (per-user read receipts for free — the M34 machinery needed zero changes).
- `Notifications.Read` policy now admits ClaimsAdjuster.
- Legacy Infrastructure gained a **named transitional dispatcher seam** reference to
  `Modules.Claims.Domain` (mirror of the M37 Underwriting.Domain seam — both retire together
  when the dispatcher moves to self-describing envelopes).

## Decisions and why

**Zero new delivery machinery.** This milestone is the payoff proof for M40's plug-in registry:
seven mapper classes + seven DI lines, and the dispatcher, projector, publisher, retry/poison
path, SNS envelope, and team read receipts all just work. The event spine absorbed a whole new
bounded context without being edited.

**One audience per event.** Following the evidence precedent (each event notifies the party who
must *react*): filings and responses go to the team (the department works from its queue);
assignment/info-request/decisions go to the claimant. The adjuster personally learns of claimant
responses through the team inbox — there is no per-user "notify the assigned adjuster" channel
yet; the response event already carries `assignedAdjusterUserId` so a future personal channel
needs no event change.

**`ClaimDocumentUploadedDomainEvent` stays unmapped** (a processed no-op): a team ping per
uploaded file is noise; the adjudication detail shows documents. Recorded so nobody hunts for a
"missing" mapper.

**Amounts in attributes are invariant-culture strings** (`300000.00`) — same reasoning as the
CM4 timeline formatting; attribute payloads end up in JSON and SNS envelopes and must not vary
by server locale.

## Verification

- Full backend suite: **177 unit + 236 integration passed**, 4 skipped (opt-in), zero warnings.
- New coverage: 13 tests — 7 mapper facts (type/audience/subject/attributes incl.
  `actionRequired` and settlement formatting), 5 role→audience matrix facts (ClaimsAdjuster
  isolation, Underwriter unchanged, Admin superset, customers none, union), 1 projector fact
  (shared claims-operations entry, idempotent, no personal entry leak).
- No migration — the notifications tables are audience-agnostic by design (M34).

## Intentionally not built yet

Personal notify-the-adjuster channel (see above), notification UI badges for the claims pages
(CM7 renders the existing notifications page which now simply shows the new entries), digest/
email delivery (a publisher concern, not a mapper concern).
