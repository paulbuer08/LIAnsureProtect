# Milestone 20 - Quote Acceptance And Policy Binding Foundation Learnings

This document records the implementation notes, design decisions, test coverage, and verification path for `Milestone 20 - Quote Acceptance And Policy Binding Foundation`.

Status: implemented.

Branch:

```text
codex/milestone-20-quote-acceptance-and-policy-binding-foundation
```

Starting point:

```text
811f459 docs: close external rating provider resilience milestone
```

## Plain-English Goal

Milestone 20 adds the first safe path from quote to policy.

In insurance terms:

- quote creation says, "Here is the price and proposed coverage the system can offer or refer";
- quote acceptance says, "The customer or broker wants to proceed with this quote and acknowledges the subjectivities";
- policy binding says, "Coverage is now in force according to the recorded local quote terms."

This milestone is deliberately more realistic than a simple status change. It records an acceptance attestation, creates a durable policy record, records a simulated binding acknowledgement, protects both high-risk POST actions with idempotency, and emits a policy-bound event into the transactional outbox.

## Implemented Scope

Implemented endpoints:

```text
POST /api/v1/quotes/{quoteId}/accept
POST /api/v1/quotes/{quoteId}/bind
```

Implemented authorization policies:

```text
Quotes.Accept  -> Customer, Broker, Admin
Policies.Bind  -> Customer, Broker, Admin
```

Implemented storage:

```text
quotes
  accepted_by_user_id
  accepted_by_name
  accepted_by_title
  subjectivities_acknowledged
  accepted_at_utc

policies
  id
  quote_id
  submission_id
  owner_user_id
  policy_number
  premium
  requested_limit
  retention
  effective_date_utc
  expiration_date_utc
  status
  bound_by_user_id
  bound_at_utc
  created_at_utc
  quote_status_at_bind
  quote_risk_tier_at_bind
  quote_subjectivities_at_bind

policy_binding_attempts
  id
  policy_id
  provider_name
  status
  binding_reference
  failure_reason
  created_at_utc
  completed_at_utc
```

The policy number format is:

```text
LIP-CYB-{yyyyMMdd}-{8 uppercase hex}
```

The first policy lifecycle status is:

```text
Bound
```

## State Rules

Quote acceptance allows:

```text
Quoted -> Accepted
Approved -> Accepted
```

Quote acceptance rejects:

```text
Referred
Declined
Accepted
Bound
Expired quotes
Requests without subjectivity acknowledgement
```

Policy binding allows:

```text
Accepted quote -> Bound policy
Accepted quote -> quote status Bound
```

Policy binding rejects:

```text
Quoted
Referred
Approved but not accepted
Declined
Bound
Cross-owner quotes
Quotes that already have a policy
```

This preserves underwriting authority. A referred quote cannot be accepted or bound until an underwriter approves or adjusts it through the Milestone 18 underwriter workflow.

## Why Binding Stays Local

Milestone 19 added external rating provider indications, but those indications are not bindable coverage. Milestone 20 keeps the local quote authoritative:

```text
local quote terms
  -> accepted by customer/broker/admin
  -> local policy record
  -> simulated binding acknowledgement
  -> PolicyBoundDomainEvent
```

The simulated binding provider is intentionally not a real carrier integration. It gives the app the shape of a carrier bind acknowledgement and an audit row without requiring real carrier credentials, production onboarding, filed forms, or legal policy wording.

## Idempotency

Both accept and bind are high-risk protected POST actions, so both use the existing PostgreSQL-backed `Idempotency-Key` pattern.

Safe accept retry:

```text
same user
same Idempotency-Key
same quote id
same request body
  -> same stored 200 OK response
  -> quote is not accepted twice
```

Safe bind retry:

```text
same user
same Idempotency-Key
same quote id
same effective date
  -> same stored 201 Created response
  -> one policy
  -> one binding attempt
  -> one PolicyBoundDomainEvent outbox row
```

Unsafe reuse:

```text
same Idempotency-Key
different user, action, route, or body
  -> 409 Conflict
```

## Deferred Scope

The following are real specialty-insurance capabilities, but they remain separate milestones:

- Stripe test-mode payment collection.
- Production policy document generation.
- Real carrier binding APIs.
- Real broker/customer e-signature envelopes.
- Endorsements.
- Cancellations.
- Renewals.
- Reinstatements.
- Claims.
- Billing and collections.
- SNS/SQS notification publishing.
- Notification inboxes.
- Advisory AI underwriting assistance.

Stripe is feasible in test mode, but it adds checkout sessions, secrets, redirect flow, webhook verification, payment reconciliation, and payment failure states. SNS/SQS are also feasible, but they should build on the existing outbox after the policy-bound event exists. AI remains advisory only and must not approve, deny, bind, issue, or close insurance decisions.

## Files Added Or Updated

Domain:

```text
src/LIAnsureProtect.Domain/Quotes/Quote.cs
src/LIAnsureProtect.Domain/Quotes/QuoteStatus.cs
src/LIAnsureProtect.Domain/Policies/*
```

Application:

```text
src/LIAnsureProtect.Application/Quotes/Commands/AcceptQuote/*
src/LIAnsureProtect.Application/Policies/*
src/LIAnsureProtect.Application/Quotes/IQuoteRepository.cs
src/LIAnsureProtect.Application/Common/Security/ApplicationPolicies.cs
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Policies/*
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/PolicyConfiguration.cs
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/PolicyBindingAttemptConfiguration.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260621125704_AddQuoteAcceptanceAndPolicyBinding.cs
src/LIAnsureProtect.Infrastructure/Persistence/SubmissionDbContext.cs
```

API:

```text
src/LIAnsureProtect.Api/Controllers/QuotePolicyBindingController.cs
src/LIAnsureProtect.Api/Security/AuthorizationPolicies.cs
```

Tests:

```text
tests/LIAnsureProtect.UnitTests/Policies/PolicyBindingTests.cs
tests/LIAnsureProtect.IntegrationTests/QuotePolicyBindingEndpointTests.cs
tests/LIAnsureProtect.IntegrationTests/DependencyRegistrationTests.cs
```

## Verification

Verification path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

Result after implementation:

```text
Focused policy binding unit tests: 7 passed
Focused quote acceptance/policy binding integration tests and migration guard: 15 passed
Build: succeeded with 0 warnings and 0 errors
Direct solution test run:
  UnitTests: 37 passed
  IntegrationTests: 57 passed, 1 skipped PostgreSQL opt-in test
EF Core pending model check: no pending model changes
Local CI: passed
Local CI UnitTests: 37 passed
Local CI IntegrationTests: 58 passed, including the PostgreSQL opt-in persistence test
Frontend Vitest: 5 files passed, 16 tests passed
Artifact zip: TestResults\local-ci-20260621-210031.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- All committed migrations applied, including `20260621125704_AddQuoteAcceptanceAndPolicyBinding`.
- Backend build passed with 0 warnings and 0 errors.
- Backend unit and integration tests passed.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed.
- CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.

## Closeout

Milestone 20 is complete.

Implementation commit:

```text
ade6297 feat: add quote acceptance and policy binding foundation
```

Final verification artifact:

```text
TestResults\local-ci-20260621-210031.zip
```

Recommended next milestone:

```text
Milestone 21 - Notification And Outbox Publishing Foundation
```

The next milestone should put the existing transactional outbox to practical use by publishing provider-shaped notification messages for important quote and policy workflow events. Real payment collection, production policy documents, real carrier APIs, real e-signature, claims, billing, and AI underwriting assistance should remain separate milestones because they require separate data models, external integration decisions, failure handling, and legal or operational workflows.
