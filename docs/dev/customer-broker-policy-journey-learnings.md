# Customer/Broker Policy Journey — Learnings

## What shipped

This slice closed the gap between a technically bound Policy and a customer experience that treats
the Policy as the primary contract. It delivered role-correct notifications, owner-scoped policy
reads and pages, combined journey presentation, and audited Submission cleanup controls without
weakening the modular-monolith or transactional-outbox boundaries.

## The state model that prevented false history

Submission, Quote, and Policy keep separate statuses. A bound Policy does not rewrite its source
Submission from `Submitted`; the UI instead shows all three records and a derived journey-stage
label. Policy reads expose `contractualStatus` (persisted, currently `Bound`) and `coverageState`
(computed as Scheduled/Active/Expired from the coverage instants). This avoids pretending `Active`
is persisted or creating a scheduler/state transition that the domain does not have.

## Ownership remains the final boundary

`GET /api/v1/policies` and `GET /api/v1/policies/{id}` join Policy to its source Submission in an
`AsNoTracking` projection filtered by the caller's `OwnerUserId`. Another owner's id returns 404.
Admin is role-authorized but not silently exempt from ownership; an on-behalf-of workflow needs its
own later audit contract.

The Policy detail page asks the Claims context's existing claimable-policy endpoint whether **File
claim** is eligible. Policy UI does not duplicate Claims rules.

## Notification role plumbing was already secure

The backend already mapped server-authoritative roles to allowed team audiences. The defect was UI
presentation: every role saw tabs. The page now consumes the shared `/me` role query, renders no tabs
for Customer/Broker personal-only access, retains All/Personal/Team for operational roles, and resets
an invalid filter when capability changes. Subject type now drives action labels and routes.

## Audit history means asymmetric cleanup

- Draft is not submitted business: the owner may delete it after confirmation.
- Submitted is audit history: it is retained and may transition idempotently to Withdrawn only before
  a Quote is Accepted or Bound.
- Withdrawal records `SubmissionWithdrawnDomainEvent`; state plus outbox row commit atomically.
- Quote decline/expiry is not Submission withdrawal.
- A bound Policy is never deleted. Cancellation was not added because a real cancellation needs an
  effective instant, reason, audit event, Claims eligibility implications, and notifications.

Duplicate detection is advisory. A same-owner, same-company open record sets `possibleDuplicate`,
but the API still creates the new draft because renewals, different products/entities, replacement
applications, and Broker client work make multiple submissions legitimate.

## Verification

The final clean-runner path passed without weakening tests:

- `dotnet build LIAnsureProtect.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-build`: UnitTests 195 passed; IntegrationTests 256 passed
  with 4 intentional service opt-in skips.
- EF Core pending-model checks: clean for `SubmissionDbContext`, `NotificationsDbContext`,
  `UnderwritingDbContext`, and `ClaimsDbContext`.
- Frontend: TypeScript, ESLint, production build, and all 84 tests passed across 17 files.
- Full Docker-backed local CI: all four migration sets applied to fresh PostgreSQL; UnitTests 195
  passed; IntegrationTests 257 passed with 3 intentional service skips; frontend build, lint, and all
  84 tests passed; artifact creation and Docker cleanup completed. The script printed
  `Local CI passed.` and wrote `TestResults/local-ci-20260711-010323.zip`.

Two older integration-test hosts constructed three outbox-source contexts while the dispatcher now
has four. They were corrected to replace `ClaimsDbContext` with SQLite as well, keeping clean-runner
tests isolated from a developer PostgreSQL instance. Assertions and production behavior were not
weakened.
