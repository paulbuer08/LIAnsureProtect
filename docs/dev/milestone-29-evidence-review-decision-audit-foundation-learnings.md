# Milestone 29 - Evidence Review Decision Audit Foundation Learnings

## What This Milestone Added

Milestone 29 added a human evidence sufficiency review layer after Milestone 28 document security screening.

Milestone 28 answered:

```text
Is this uploaded document safe enough for the application to trust and stream?
```

Milestone 29 answers:

```text
Does the trusted evidence actually satisfy the underwriter's request?
```

This milestone is intentionally about underwriter review and audit evidence. It does not approve quotes, bind policies, automate underwriting, run OCR, use embeddings, or let AI decide whether evidence is sufficient.

## Final Scope

Underwriters can now record one of these current review decisions on a responded evidence request:

- `NotReviewed`
- `Satisfied`
- `Insufficient`
- `NeedsClarification`

`NotReviewed` is the default. It means the owner has not yet received an underwriter sufficiency decision for the current response.

`Satisfied` means the evidence request is accepted. This maps to the existing accepted evidence lifecycle so Milestone 25 and Milestone 26 behavior still works.

`Insufficient` means the underwriter reviewed the response and found that it does not meet the requested evidence standard.

`NeedsClarification` means the response may be relevant, but the underwriter needs clearer explanation, scope, or supporting context before treating it as sufficient.

For every decision, the app records:

- the decision
- underwriter reason text
- optional owner-facing remediation guidance for `Satisfied`
- required owner-facing remediation guidance for `Insufficient` and `NeedsClarification`
- reviewer user id
- reviewed timestamp

## Why Current State Plus Audit History

The milestone stores current review state on `quote_evidence_requests` and also writes append-only history to `quote_evidence_request_reviews`.

That split is intentional.

The current fields answer fast screen questions:

```text
What should the owner see now?
Does this evidence request need more owner work?
Is this response waiting for review?
Can the workbench count satisfied, unreviewed, and follow-up evidence?
```

The audit table answers history questions:

```text
Who reviewed this request?
What did they decide?
What reason did they give?
What owner guidance was sent?
How many documents existed at review time?
How many of those documents were clean at review time?
```

This is closer to real specialty insurance workflow than overwriting one status field only. Current state keeps operational screens simple; audit history preserves what happened.

## Storage Shape

`quote_evidence_requests` now includes current review fields:

```text
review_decision
review_reason
remediation_guidance
reviewed_by_user_id
reviewed_at_utc
```

`quote_evidence_request_reviews` stores append-only review history:

```text
id
evidence_request_id
quote_id
submission_id
owner_user_id
category
decision
reason
remediation_guidance
reviewed_by_user_id
reviewed_at_utc
document_count
clean_document_count
```

The document counters are important audit evidence. They show whether the underwriter reviewed a text-only response, a clean document-backed response, or a response that had documents blocked by security screening.

## API Shape

Milestone 29 added this underwriter-only endpoint:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/review-decision
```

Request body:

```json
{
  "decision": "NeedsClarification",
  "reason": "The response does not confirm privileged account MFA scope.",
  "remediationGuidance": "Please confirm whether MFA applies to all administrator and service-owner accounts."
}
```

Rules:

- `decision` must be `Satisfied`, `Insufficient`, or `NeedsClarification`.
- `NotReviewed` is a system state, not a posted review decision.
- `reason` is required.
- `remediationGuidance` is required for `Insufficient` and `NeedsClarification`.
- The evidence request must be `Responded`.
- Customer and broker users cannot record review decisions.

The existing `/accept` endpoint remains as a compatibility path. It records a `Satisfied` review decision and preserves the already existing accepted evidence notification/outbox behavior.

`/cancel` remains cancellation. It is not a sufficiency review decision.

## Clean Document Gate

Milestone 29 keeps the Milestone 28 trust boundary.

If a response has attached documents, every attached document must be `Clean` before the underwriter can record a trusted review decision.

That means these documents block review:

- `PendingScan`
- `Rejected`
- `Failed`

Text-only responses can still be reviewed, because there is no document trust decision to wait for.

This distinction matters in real insurance workflow:

- Security screening decides whether file bytes can be trusted.
- Evidence review decides whether the business content satisfies the underwriting request.

Those are related but not the same responsibility.

## Owner Remediation Path

When underwriters record `Insufficient` or `NeedsClarification`, the owner evidence page shows:

- the review decision
- the underwriter reason
- the remediation guidance
- reviewer metadata when present

Owners can then submit supplemental evidence. That supplemental response:

- keeps the original evidence request id
- appends optional newly scanned documents
- resets current review state to `NotReviewed`
- preserves prior `quote_evidence_request_reviews` rows

The reset is important. It means the latest owner response is waiting for underwriter review, while the previous unfavorable decision remains visible in immutable audit history.

## Frontend Changes

The underwriting workbench now shows evidence review status more clearly:

- unreviewed responded evidence count
- satisfied evidence request count
- evidence requests needing attention
- review decision controls for `Satisfied`, `Insufficient`, and `NeedsClarification`
- reason and owner remediation guidance inputs

The owner evidence page now shows the latest review outcome and displays the supplemental evidence form when more information is needed.

Milestone 28 download gating remains unchanged: only clean documents render download links.

## Verification

Focused verification during implementation:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore --filter QuoteEvidenceRequestTests
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter "Evidence_Request_Review_Decision|Underwriter_Cannot_Record_Evidence_Review_Decision|PersistenceMigrationsCreatePgvectorExtensionAndSubmissionsTable"
```

Broader verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
```

Results:

- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 56 passed and IntegrationTests 89 passed, 1 skipped PostgreSQL opt-in test.
- EF Core pending model check reported no pending model changes.
- Focused frontend evidence/workbench tests passed with 2 files and 13 tests.
- Direct frontend TypeScript/Vite production build passed.
- Full local CI passed with UnitTests 56 passed, IntegrationTests 90 passed against the fresh Docker PostgreSQL path, frontend build/lint/tests, Docker Compose validation, and artifact creation.
- Full local CI artifact: `TestResults\local-ci-20260623-173225.zip`.

## Deferred Scope

These are still intentionally out of scope:

- OCR/document extraction
- autonomous AI evidence review
- embeddings or RAG
- production S3 document management
- durable document download audit
- legal hold or retention automation
- manual malware analyst workflow
- multi-reviewer approval chains
- quote approval automation
- policy binding
- changing premium, retention, subjectivities, or final quote decision from evidence review

Evidence review should inform underwriting readiness. It should not become final underwriting authority.
