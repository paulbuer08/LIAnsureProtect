# Milestone 39 - Quoting Decision Boundary Design

Date: 2026-07-02

## 1. Goal

Milestone 39 starts the Quoting-side boundary work that M35-M38 deliberately deferred.

The Underwriting module now owns operational underwriting work:

- advisory AI review;
- referral operations;
- evidence requests and reviews;
- evidence document metadata and document trust gates.

The final quote decision still belongs to the `Quote` aggregate:

- `Quote.ApproveReferral(...)`;
- `Quote.DeclineReferral(...)`;
- `Quote.AdjustReferral(...)`;
- `QuoteUnderwritingReview`;
- the `quote_underwriting_reviews` table;
- the `QuoteUnderwritingDecisionRecordedDomainEvent`.

Do not move final approve/decline/adjust into Underwriting just because the user interface is the
underwriting workbench. Those commands mutate quote terms, quote status, subjectivities, premium, and
final underwriting decision state. That is Quoting authority.

M39 should make that boundary explicit and prepare the Quoting carve without starting a broad quote/rating
module migration.

## 2. Current State After M38

The system is a modular monolith with three EF Core contexts:

```text
SubmissionDbContext       -> legacy public schema, still owns quotes and quote decision audit
NotificationsDbContext    -> notifications schema
UnderwritingDbContext     -> underwriting schema, owns AI/referral/evidence operational state
```

The remaining decision seam looks like this:

```text
Underwriting workbench / API route
  -> UnderwritingQuoteReferralsController
  -> legacy Application command handler
  -> IQuoteRepository
  -> Quote.ApproveReferral / DeclineReferral / AdjustReferral
  -> QuoteUnderwritingReview audit row
  -> SubmissionDbContext SaveChangesAsync
  -> legacy outbox captures QuoteUnderwritingDecisionRecordedDomainEvent
  -> dispatcher projects close/status/timeline changes into Underwriting referral operations
```

This is acceptable today because the final decision is still quote-owned. It is also the next boundary to
clarify because Underwriting now consumes the decision outcome as an event while Quoting owns the mutation.

## 3. Boundary Decision

M39 should introduce a Quoting-facing decision boundary rather than moving the decision into Underwriting.

Recommended shape:

```text
src/Modules/Quoting/
  LIAnsureProtect.Modules.Quoting.Domain
  LIAnsureProtect.Modules.Quoting.Application
  LIAnsureProtect.Modules.Quoting.Infrastructure
```

Keep the first slice narrow:

- create the Quoting module projects and register them in the solution;
- move or wrap final referral decision use cases behind Quoting Application contracts;
- keep the `Quote` aggregate and quote tables in `SubmissionDbContext` unless the implementation plan
  proves a table move can stay small and always-green;
- make Underwriting/host code call the Quoting decision boundary rather than treating the decision as an
  Underwriting command;
- preserve the existing public API routes and React behavior.

This is a boundary-preparation milestone. It should not try to complete the full Quoting carve if that
would require moving rating attempts, policies, acceptance, binding, idempotency, and owner quote reads in
one PR.

## 4. What Must Stay True

- The Underwriting module must not reference legacy Domain/Application/Infrastructure or another module.
- Quoting must own final quote decision authority.
- Underwriting can continue to consume final decision events through the dispatcher/projector seam.
- Public routes stay stable:
  - `POST /api/v1/underwriting/quote-referrals/{quoteId}/approve`
  - `POST /api/v1/underwriting/quote-referrals/{quoteId}/decline`
  - `POST /api/v1/underwriting/quote-referrals/{quoteId}/adjust`
- Existing authorization stays stable: underwriter/admin authority through the current `Quotes.Underwrite`
  policy.
- Existing audit behavior stays stable: one decision writes one `QuoteUnderwritingReview` row and one
  outbox event.
- Existing idempotency and quote-state guards stay stable.

## 5. Explicitly Out Of Scope

- Moving all quote/rating/policy tables into a new schema.
- Changing quote premium/rating algorithms.
- Moving quote acceptance or policy binding.
- Moving owner quote creation/read endpoints.
- Replacing Auth0 or authorization policy names.
- Introducing external carrier decision APIs.
- Changing the frontend workbench behavior beyond namespace/type wiring required by the boundary.
- Dispatcher integration-event registry/decoupling; keep that for M40 unless the M39 implementation makes
  a small preparatory refactor unavoidable.

## 6. Testing Strategy

Use the existing tests as the behavior contract:

- `QuoteUnderwritingReviewTests` for aggregate decision behavior;
- `UnderwritingReferralEndpointTests` for route authorization, decision transitions, audit rows, and
  referral-operation projection;
- architecture tests for exact project-reference boundaries;
- migration pending-model checks for all three existing contexts.

If M39 adds a new Quoting module project, update `ProjectReferenceBoundaryTests` with exact allowed edges
only. Do not weaken the module-boundary ratchet.

## 7. Verification

M39 should use the same closeout gates:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```
