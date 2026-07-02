# Milestone 39 - Quoting Decision Boundary - Learning Notes

## Goal

Milestone 39 made the final quote referral decision boundary explicit without moving quote tables yet.

The key decision was to avoid pushing approve, decline, and adjust behavior into the Underwriting module.
Those actions still change quote terms and final quote state:

- quote status;
- premium;
- retention;
- subjectivities;
- reviewed-by and reviewed-at fields;
- `QuoteUnderwritingReview` audit rows;
- `QuoteUnderwritingDecisionRecordedDomainEvent`.

That is Quoting authority, even though the HTTP routes are under the underwriting workbench.

## What changed

M39 added the Quoting module shape:

```text
src/Modules/Quoting/
  LIAnsureProtect.Modules.Quoting.Domain
  LIAnsureProtect.Modules.Quoting.Application
  LIAnsureProtect.Modules.Quoting.Infrastructure
```

The first slice is intentionally small. It creates the module boundary and moves the final referral
decision command surface into Quoting Application:

```text
Modules.Quoting.Application/ReferralDecisions
  ApproveQuoteReferralCommand
  DeclineQuoteReferralCommand
  AdjustQuoteReferralCommand
  validators
  handlers
  UnderwriteQuoteReferralResult
  IQuoteReferralDecisionService
```

The public routes stayed stable:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/approve
POST /api/v1/underwriting/quote-referrals/{quoteId}/decline
POST /api/v1/underwriting/quote-referrals/{quoteId}/adjust
```

The controller now sends Quoting module commands, but the URL remains an underwriting workbench URL.
That keeps existing React behavior and API callers stable while making the owner of the decision clearer
inside the code.

## Why the Quote aggregate did not move yet

The `Quote` aggregate still lives in the legacy Domain and the quote tables still live in
`SubmissionDbContext`.

That is deliberate. Moving `Quote` safely is larger than moving the three command classes. The current
quote area also includes:

- local rating;
- external rating-provider attempt audit;
- quote acceptance;
- policy binding;
- owner quote reads;
- idempotency around quote actions;
- outbox capture from `SubmissionDbContext`.

Trying to move all of that in one milestone would turn a boundary clarification into a broad Quoting
carve. M39 instead used a Quoting-owned port:

```text
Quoting Application command handler
  -> IQuoteReferralDecisionService
  -> legacy Infrastructure adapter
  -> IQuoteRepository
  -> Quote.ApproveReferral / DeclineReferral / AdjustReferral
  -> QuoteUnderwritingReview row
  -> SubmissionDbContext.SaveChangesAsync
  -> legacy outbox captures QuoteUnderwritingDecisionRecordedDomainEvent
```

The module owns the request/handler contract. The legacy adapter owns the temporary persistence seam
while the database still belongs to `SubmissionDbContext`.

## Why this does not violate the module boundary

The Quoting module does **not** reference legacy Domain, Application, or Infrastructure.

The direction is:

```text
Modules.Quoting.Application
  -> defines IQuoteReferralDecisionService

LIAnsureProtect.Infrastructure
  -> implements IQuoteReferralDecisionService while quote persistence remains legacy
```

This is the same strangler pattern used in earlier module work: a module owns the port, and the legacy
side implements it until the data and aggregate move. The module-boundary ratchet still proves modules
do not reference other modules or legacy layers.

## Underwriting remains operational

Underwriting still owns referral operations, evidence requests, evidence reviews, and evidence document
trust state. It does not own final quote decisions.

After a final decision, Underwriting reacts through events:

```text
Quote decision recorded
  -> QuoteUnderwritingDecisionRecordedDomainEvent
  -> outbox dispatcher
  -> IReferralOperationProjector
  -> underwriting.quote_referral_operations closes
  -> timeline records final decision activity
```

M39 strengthened integration coverage for approve, decline, and adjust. The tests pump the outbox before
asserting module operation state, then prove the operation is closed and the timeline contains the final
decision projection. This keeps the eventual-consistency rule explicit instead of hiding it behind sleeps
or weaker assertions.

## The cleanup guard

A new architecture ratchet prevents the old legacy command path from returning:

```text
src/LIAnsureProtect.Application/Quotes/Commands/UnderwriteQuoteReferral
```

The test asserts that no `.cs` files remain there and that the three decision command records live under
the Quoting module. This is a small but useful guard because duplicate command paths would make it unclear
which boundary owns the final decision.

## Verification

Fresh verification for M39 closeout:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```

Results:

- build passed with 0 warnings and 0 errors;
- full solution tests passed with UnitTests 66 and IntegrationTests 124, with one PostgreSQL opt-in test
  skipped by design;
- all three EF Core pending-model checks reported no pending model changes;
- full local CI passed against fresh Docker PostgreSQL, all three context migration sets, backend tests,
  frontend install/build/lint/tests, artifact creation, and Docker cleanup;
- local CI artifact: `TestResults\local-ci-20260702-145319.zip`.

## What is next

Milestone 40 should keep the existing roadmap direction: dispatcher integration-event decoupling and
mapper registry work.

M39 made the Quoting decision boundary clearer, but the dispatcher is still centralized in legacy
Infrastructure and still knows concrete domain event types from multiple contexts. M40 can improve that
handoff shape without moving quote tables yet.
