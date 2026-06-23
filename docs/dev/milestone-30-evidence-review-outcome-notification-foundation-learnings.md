# Milestone 30 - Evidence Review Outcome Notification Foundation Learnings

## What This Milestone Added

Milestone 30 connects Milestone 29 evidence review outcomes to the existing local notification and outbox foundation.

Milestone 29 answered:

```text
What did the underwriter decide about the evidence, and what audit trail proves it?
```

Milestone 30 answers:

```text
If the decision requires more customer or broker action, how does that action become operationally visible?
```

The milestone adds a remediation-required notification for unfavorable evidence review decisions:

- `Insufficient`
- `NeedsClarification`

It intentionally does not create a new notification delivery system. The project already has transactional outbox capture, a Worker-side outbox dispatcher, an Application-owned notification boundary, and a local notification publisher from Milestones 21 and 26. This milestone reuses that path.

## Why The Notification Is Unfavorable-Only

`Satisfied` evidence already maps to the accepted evidence lifecycle.

That path still raises:

```text
QuoteEvidenceRequestAcceptedDomainEvent
```

and the outbox mapper still publishes:

```text
evidence_request.accepted
```

`Insufficient` and `NeedsClarification` are different. They mean the owner has more work to do before the evidence request can support underwriting readiness.

For those outcomes, the domain now raises:

```text
QuoteEvidenceRequestRemediationRequiredDomainEvent
```

The outbox dispatcher maps it to:

```text
evidence_request.remediation_required
```

Simple analogy:

```text
Satisfied:
  "The underwriter accepted the paperwork."

Insufficient or NeedsClarification:
  "The underwriter reviewed the paperwork, but the owner needs to fix or explain something."
```

That split is closer to how real specialty workflows operate. Acceptance can close the request. Remediation needs a clear action signal back to the customer or broker.

## Notification Payload Shape

The notification is action-oriented without exposing unsafe document details.

It includes safe workflow attributes:

```text
evidenceRequestId
quoteId
submissionId
requestedByUserId
category
status
dueAtUtc
reviewedByUserId
decision
reviewReason
remediationGuidance
actionRequired
```

The important user-facing fields are:

- `decision`: either `Insufficient` or `NeedsClarification`
- `reviewReason`: why the underwriter could not treat the response as sufficient
- `remediationGuidance`: what the owner should provide or clarify next
- `actionRequired`: `true`

The notification does not include:

- document content
- storage keys
- file bytes
- raw uploaded file names
- production email/SMS provider details
- notification preference data
- broker chat or messaging-thread data

This matters because notification events often become inputs to later inboxes, email templates, SMS templates, webhooks, or audit exports. Keeping the payload safe now prevents accidental leakage later.

## Flow

The implemented flow is:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/review-decision
  -> underwriter records Insufficient or NeedsClarification
  -> QuoteEvidenceRequest updates current review state
  -> QuoteEvidenceRequestReview audit row is inserted
  -> QuoteEvidenceRequestRemediationRequiredDomainEvent is recorded
  -> SubmissionDbContext saves the event to outbox_messages
  -> OutboxDispatcher maps the event to evidence_request.remediation_required
  -> local notification publisher records provider-style publish metadata
```

The event is emitted by the domain object because the domain knows when an unfavorable review decision has been accepted as valid state. The mapper stays in Infrastructure because it translates durable outbox messages into provider-shaped notification messages.

## Why There Is No New Migration

No new database table or column is needed.

The existing `outbox_messages` table already stores domain events as serialized payloads. The new event is another domain event type captured by the same outbox mechanism.

That means the EF Core pending-model check should report no pending model changes.

## Why This Is Still Local Only

This milestone proves the business event and notification mapping, not production delivery.

Still out of scope:

- production SNS/SQS publishing
- production email/SMS delivery
- notification inboxes
- notification preferences
- scheduled reminder automation
- broker/customer chat
- OCR or document extraction
- autonomous AI evidence review
- embeddings or RAG
- policy binding or final quote approval automation

Those are real product directions, but each one adds separate state, delivery failure modes, authorization rules, and user experience decisions.

## Verification

Focused verification during implementation:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore --filter QuoteEvidenceRequestTests
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter "OutboxDispatcherTests|Evidence_Request_Review_Decision_Persists_Audit_And_Exposes_Owner_Remediation"
```

Results:

- Focused domain tests passed with 11 tests.
- Focused outbox and endpoint integration tests passed with 9 tests.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 57 passed and IntegrationTests 90 passed, 1 skipped PostgreSQL opt-in test.
- EF Core pending-model check reported no pending model changes.
- Full local CI passed with UnitTests 57 passed, IntegrationTests 91 passed against the fresh Docker PostgreSQL path, frontend build/lint/tests, Docker Compose validation, artifact creation, and Docker cleanup.
- Full local CI artifact: `TestResults\local-ci-20260623-185058.zip`.

Broader verification commands:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

## What To Remember

Evidence review now has three separate but connected records:

```text
quote_evidence_requests:
  current review state for screens and workflow

quote_evidence_request_reviews:
  append-only review audit history

outbox_messages:
  notification work created from meaningful review outcomes
```

This keeps the workflow realistic without overbuilding a full messaging product. The owner can see remediation guidance on the evidence page, and the system also has an operational signal that later delivery channels can use.
