# Milestone 37 - Underwriting Evidence - Learning Notes

## Goal

Continue the Underwriting carve by moving **evidence requests and evidence reviews** out of the
legacy `Quotes` context into the Underwriting module and the `underwriting` PostgreSQL schema, while
leaving evidence document metadata/storage/scanning in legacy for the next slice. This was the first
module carve where the module itself became an event source.

## What moved

- `QuoteEvidenceRequest`, `QuoteEvidenceRequestReview`, evidence request/review enums, and evidence
  lifecycle events moved into `src/Modules/Underwriting/.../Evidence`.
- `UnderwritingDbContext` now owns `quote_evidence_requests`, `quote_evidence_request_reviews`, and
  the module `outbox_messages` table in the `underwriting` schema.
- Create/cancel/follow-up request commands and request/read summaries are module-owned.
- Legacy document handlers still own uploaded document rows and bytes for M37.

The legacy request/review tables are dropped by `DropEvidenceRequests`. The document table keeps
`evidence_request_id` as a scalar correlation id until Milestone 38 moves documents.

## Module outbox and ordered dispatch

M37 introduced a reusable module outbox row (`ModuleOutboxMessage`) plus source abstractions:

```text
SubmissionDbContext outbox_messages      (legacy quote/submission events)
UnderwritingDbContext underwriting.outbox_messages  (module evidence events)
        │
        ▼
IOutboxSource[]
        │
        ▼
OutboxDispatcher merge-orders by CreatedAtUtc
        ├─ notifications
        └─ referral-operation projection
```

The key behavior is **merge ordering** across sources. A module evidence-created event that was saved
at `T` must be processed before a legacy underwriting-decision event saved at `T+1`, even if the
dispatcher happens to query the legacy source first. The integration test now covers that exact
module-evidence-before-legacy-decision case.

## The important seam correction

The original cutover shape assumed legacy document handlers could call a writer after storing
documents, but the document code also needed request facts *before* storage: quote id, submission id,
owner user id, and request status. The right seam was not a cross-module aggregate reference. It was a
primitive lookup/snapshot method on the module reader:

```text
legacy document handler
  -> IEvidenceRequestsReader.GetOwnerRequestAsync / GetUnderwritingRequestAsync
  -> store and scan legacy documents using primitive facts
  -> IEvidenceRequestWriter.RecordResponse / Accept / RecordReviewDecision
```

That keeps the modular-monolith boundary intact. Legacy can consume module Application ports during
the strangler, but the module still does not reference legacy, and document storage never gets a live
module aggregate.

## The document FK had to move earlier than planned

The plan placed the document-to-request FK drop in the final migration task. During B8 test rework,
uploads failed because `quote_evidence_documents.evidence_request_id` still had a database FK to the
legacy `quote_evidence_requests` table, while new requests were already module-owned. That was a real
code issue, not a test timing issue.

The fix was to split the FK removal into its own migration:

```text
DropEvidenceDocumentRequestForeignKey
  -> drops quote_evidence_documents -> quote_evidence_requests FK only

DropEvidenceRequests
  -> drops quote_evidence_request_reviews
  -> drops quote_evidence_requests
```

This preserved the plan's end state while keeping B8's document upload path honest.

## Supplemental evidence needed a distinct module method

Replacement uploads are allowed after a rejected or failed document scan even when no underwriter has
yet recorded `Insufficient` or `NeedsClarification`. Reusing the normal `Respond(...)` method for
replacement uploads was too strict because the aggregate's normal response guard is about first
responses or unfavorable review remediation.

The module aggregate now has a dedicated supplemental path:

```text
RecordResponseAsync              -> QuoteEvidenceRequest.Respond(...)
RecordSupplementalResponseAsync  -> QuoteEvidenceRequest.RecordSupplementalResponse(...)
```

Both paths still reset review state to `NotReviewed` and raise a responded event, but they express two
different workflow reasons. This kept the existing replacement-document behavior without weakening
tests or making the normal response rule looser.

## Tests that mattered

B8 restored the suite after the strangler cutover by changing tests to seed/read module request state
through `UnderwritingDbContext` while continuing to assert legacy document behavior through
`SubmissionDbContext`.

High-value coverage added or preserved:

- evidence document endpoint tests create module requests and assert document upload/download behavior
  still works through the legacy document handlers;
- underwriting referral evidence tests assert module request/review rows and module outbox messages;
- dispatcher tests use module evidence events instead of legacy event types;
- cross-source dispatcher ordering proves module evidence events can precede legacy quote decision
  events by `CreatedAtUtc`.

No assertion was removed to force green. The one integration failure during B8 exposed the real FK
problem above.

## Verification

- `dotnet build LIAnsureProtect.slnx --no-restore` - 0 warnings / 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-build` - UnitTests 62 passed; IntegrationTests 124 passed,
  1 PostgreSQL opt-in test skipped by design.
- `dotnet ef migrations has-pending-model-changes` - clean for `SubmissionDbContext`,
  `NotificationsDbContext`, and `UnderwritingDbContext`.
- Full local CI (`pwsh ./scripts/run-local-ci.ps1`) passed against fresh Docker PostgreSQL, backend
  tests, frontend build/lint/tests, and Docker cleanup. Artifact:
  `TestResults\local-ci-20260701-182841.zip`.

## What is next

Milestone 38 should move the **evidence document** side into the Underwriting module:

- `QuoteEvidenceDocument`;
- upload/download/replacement handlers;
- document scan metadata and clean-only gates;
- document query/read models used by owner and underwriting endpoints.

The request/review seam created in M37 should make that move smaller: request facts are already
module-owned, and document rows now reference evidence requests by id only.
