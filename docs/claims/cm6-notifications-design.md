# Claims Milestone 6 - Notifications — Design

> Branch-local doc (`docs/claims/` policy — see `claims-status.md`).

## What this milestone builds

The claim events that have been accumulating in the module outbox since CM1 finally **reach
inboxes**: claimants get personal notifications for every step that affects them (assignment,
information requests — remediation-style with `actionRequired` — decisions, closure), and the
claims department gets a **new team inbox audience `claims-operations`** (the
underwriting-operations pattern applied to the third persona) for new filings and claimant
responses. No new delivery machinery — everything rides the existing
outbox → dispatcher → mapper registry → projector/publisher spine (M40's plug-in seam doing
exactly what it was built for).

## Mapping table (one registered mapper per event)

| Domain event (Claims outbox) | Message type | Audience | Notable attributes |
|---|---|---|---|
| `ClaimFiledDomainEvent` | `claim.filed` | **claims-operations** (team) | claimNumber, policyId, policyNumber, incidentType |
| `ClaimAssignedDomainEvent` | `claim.assigned` | customer-or-broker | claimNumber, adjusterUserId |
| `ClaimInformationRequestedDomainEvent` | `claim.information_requested` | customer-or-broker | claimNumber, informationRequestId, title, **actionRequired=true** (remediation-style, like evidence) |
| `ClaimantInformationResponseDomainEvent` | `claim.information_response` | **claims-operations** (team) | claimNumber, informationRequestId, respondedByUserId, assignedAdjusterUserId |
| `ClaimAcceptedDomainEvent` | `claim.accepted` | customer-or-broker | claimNumber, settlementAmount, decidedByUserId |
| `ClaimDeniedDomainEvent` | `claim.denied` | customer-or-broker | claimNumber, denialReason, decidedByUserId |
| `ClaimClosedDomainEvent` | `claim.closed` | customer-or-broker | claimNumber, outcomeAtClose, closedByUserId |

`ClaimDocumentUploadedDomainEvent` stays deliberately unmapped (a processed no-op) — document
arrival matters to the adjuster through the detail view, and a team ping per file would be noise;
recorded so nobody hunts for a "missing" mapper.

## Changes by home

- **Notifications module (Application):** `NotificationAudiences.ClaimsOperations`
  (`"claims-operations"`), seven `NotificationMessageTypes.Claim*` constants, and
  `NotificationTeamAudiences.ForRoles` becomes role-additive: Underwriter →
  underwriting+binding (unchanged), **ClaimsAdjuster → claims-operations**, Admin → all three.
- **Notifications module (Infrastructure):** `NotificationInboxProjector` routes
  `claims-operations` to the shared team-entry path.
- **Legacy Infrastructure (the M40 registry home):** `ClaimNotificationMappers.cs` — seven
  `IOutboxMessageMapper<NotificationMessage>` classes deserializing the Claims domain events
  (new project reference Infrastructure → `Modules.Claims.Domain`, the same *named transitional
  dispatcher seam* as `Modules.Underwriting.Domain` — both retire together when the dispatcher
  moves to self-describing envelopes); DI registrations beside the evidence mappers.
- **API:** `Notifications.Read` policy gains ClaimsAdjuster (the persona needs to read the inbox
  it now has).
- **Architecture test:** Infrastructure's expected-reference list gains `Claims.Domain`.

## Testing plan (TDD)

1. Mapper tests (fake `IOutboxMessageView` carrying real serialized events): type, audience,
   subject reference, and attributes per mapper — including `actionRequired=true` on the
   information request and the settlement amount on accept.
2. `NotificationTeamAudiences` matrix: ClaimsAdjuster → claims-operations only; Underwriter →
   ops audiences only; Admin → all three; Customer → none; combined roles union.
3. Projector: `claims-operations` message → single shared team entry, idempotent on the outbox
   message id.
4. Registration: the composed service collection contains mappers for all seven claim event
   types.
