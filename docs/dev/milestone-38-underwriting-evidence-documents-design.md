# Milestone 38 - Underwriting Evidence Documents Design

Date: 2026-07-01

## 1. Goal

Milestone 38 completes the evidence-document part of the Underwriting carve started in Milestone 37.
Milestone 37 moved evidence **requests and reviews** into the Underwriting module, but deliberately left
document metadata, storage orchestration, scan metadata, replacement uploads, clean-only gates, and
downloads in the legacy quote/application layer. That was the right temporary seam because the document
handlers needed both stored file facts and request/review state.

M38 dissolves that seam:

- `QuoteEvidenceDocument` and `EvidenceDocumentScanStatus` move into the Underwriting module.
- Owner response, replacement upload, owner list with document metadata, underwriter accept/review
  gates, and owner/underwriter downloads become module Application handlers.
- Underwriting owns the document metadata table in the `underwriting` schema.
- The legacy `IQuoteRepository` stops knowing about evidence documents.
- `IEvidenceRequestWriter` disappears because request, review, and document metadata are mutated in the
  same module context again.

The HTTP API and React user experience should stay behaviorally stable: owners still upload private
evidence documents, unsafe uploads stay blocked from download and review, rejected/failed documents stay
visible for audit, replacements append new rows instead of deleting old rows, and underwriters can only
accept or satisfy evidence when every attached document is clean.

## 2. Current State After M37

The current evidence workflow is split:

```text
Owner respond / replacement upload / download / underwriter accept/review
  -> legacy Application handlers in QuoteEvidenceRequestCommands.cs
  -> legacy IQuoteRepository document methods
  -> public.quote_evidence_documents
  -> module IEvidenceRequestsReader before storage
  -> module IEvidenceRequestWriter after storage/scan/review gate
  -> underwriting.quote_evidence_requests + reviews + module outbox
```

That split is intentionally temporary. It has two important constraints:

- The Underwriting module cannot reference legacy `LIAnsureProtect.Application`.
- Storage/scanning ports currently live in legacy `Application.Documents`, and the scanner contract imports
  the legacy quote document scan enum.

So M38 cannot simply move the command handlers first. The port boundary must be corrected before the
document handlers can legally enter the module.

## 3. Boundary Decision

### Document storage is a platform port

`IDocumentStorageService` is not underwriting-specific. It is a private object-storage socket that later
switches from local filesystem to S3. Move the interface and small transport records to
`LIAnsureProtect.Platform.Abstractions.Documents`:

- `IDocumentStorageService`
- `DocumentStorageUpload`
- `StoredDocumentResult`
- `StoredDocumentDownload`

The existing `LocalDocumentStorageService` can stay in legacy Infrastructure for this milestone, but it
implements the Platform.Abstractions port. That keeps composition simple and preserves the M32 Local/AWS
profile switch. The actual S3 adapter remains deferred to the existing AWS documents milestone.

### Evidence scanning is an Underwriting concern

`IEvidenceDocumentScanner` returns evidence-document trust state and is tied to the underwriting evidence
workflow. Move it into `Modules/Underwriting/...Application/Evidence/Documents`, together with:

- `EvidenceDocumentScanRequest`
- `EvidenceDocumentScanResult`

Move the local deterministic scanner implementation into Underwriting Infrastructure. It is the module's
local adapter for the module-owned scanner port.

This split avoids a leaky "all document things are platform" design. Platform owns generic byte storage;
Underwriting owns the business decision of whether an evidence file is trusted for underwriting use.

## 4. Module Model

Move the document aggregate under the existing evidence area:

```text
src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Evidence/Documents/
  QuoteEvidenceDocument.cs
  EvidenceDocumentScanStatus.cs
```

The entity remains intentionally metadata-only:

- evidence request id
- quote id
- submission id
- owner user id
- original file name
- content type
- size
- opaque storage key
- uploaded-by user id
- uploaded timestamp
- scan status/provider/result/reason/timestamp/SHA-256

There should be no file bytes in PostgreSQL and no cross-schema foreign keys back to `quotes`,
`submissions`, or evidence requests. Cross-context correlation stays by id.

## 5. Persistence

Underwriting owns document metadata in its existing `UnderwritingDbContext`:

```text
underwriting.quote_evidence_documents
  id
  evidence_request_id
  quote_id
  submission_id
  owner_user_id
  original_file_name
  content_type
  size_bytes
  storage_key
  uploaded_by_user_id
  uploaded_at_utc
  scan_status
  scanner_provider_name
  scan_result_code
  scan_result_reason
  scanned_at_utc
  sha256
```

Use drop-and-recreate movement, consistent with M33-M37. There is no production data to migrate.

Migrations:

- `UnderwritingDbContext`: create `underwriting.quote_evidence_documents`.
- `SubmissionDbContext`: drop legacy `public.quote_evidence_documents`.

Keep the existing indexes in the new schema:

- request + uploaded-at ordering
- scan-status + uploaded-at
- owner + evidence-request
- unique storage key

## 6. Application Surface

Create a module-owned document area:

```text
Modules/Underwriting/...Application/Evidence/Documents/
  EvidenceDocumentCommands.cs
  EvidenceDocumentResults.cs
  IEvidenceDocumentRepository.cs
  IEvidenceDocumentScanner.cs
  EvidenceDocumentUploadWorkflow.cs
```

The module should own these use cases:

- `RespondToQuoteEvidenceRequestCommand`
- `UploadReplacementEvidenceDocumentsCommand`
- `AcceptQuoteEvidenceRequestCommand`
- `RecordQuoteEvidenceReviewDecisionCommand`
- `DownloadOwnerEvidenceDocumentQuery`
- `DownloadUnderwritingEvidenceDocumentQuery`

The existing document-free commands stay in the module:

- create evidence request
- cancel evidence request
- follow up evidence request
- owner evidence request list

After M38, the owner-list query should include documents from the module reader/repository, not from a
legacy quote repository call.

## 7. Transaction Boundary

M38 restores a single module database transaction for request/review metadata and document metadata:

```text
respond with documents
  -> validate uploads
  -> load module evidence request for owner
  -> store bytes through IDocumentStorageService
  -> reopen stored bytes
  -> scan through IEvidenceDocumentScanner
  -> add QuoteEvidenceDocument rows to UnderwritingDbContext
  -> call request.RecordResponse(...)
  -> SaveChangesAsync on UnderwritingDbContext
  -> module outbox captures request domain event in the same transaction
```

This is better than the M37 temporary split: the document row, request state, review audit, and module
outbox event are all saved through `UnderwritingDbContext`.

One caveat remains: file bytes are stored before the database save. If the database save fails, a local
or future S3 object may be orphaned. That is already true today. Cleanup and object lifecycle policies
belong to a later documents/S3 hardening slice, not this carve.

## 8. Clean-Only Gates

Keep the existing safety rules:

- A document is downloadable only when `ScanStatus == Clean`.
- Underwriters cannot accept/satisfy/review evidence if any attached document is pending, rejected, or failed.
- Replacement uploads are allowed only after the request has a response and at least one existing document
  is `Rejected` or `Failed`.
- Replacement uploads append new scanned rows; they never delete or overwrite rejected/failed originals.

Text-only evidence responses remain valid. If there are no documents, the clean-only gate has no unsafe
document to block.

## 9. API and Frontend Contract

Keep routes and response shapes stable:

- `POST /api/v1/evidence-requests/{evidenceRequestId}/respond`
- `POST /api/v1/evidence-requests/{evidenceRequestId}/documents/replacement`
- `GET /api/v1/evidence-requests`
- `GET /api/v1/evidence-requests/{evidenceRequestId}/documents/{documentId}/download`
- `POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/accept`
- `POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/review-decision`
- `GET /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/documents/{documentId}/download`

Controllers should change namespaces/command types, not behavior.

Frontend changes should be minimal unless TypeScript types drift. The owner page and workbench should
continue to show scan status, download availability, rejected/failed replacement options, and document
lists.

## 10. Cleanup

Remove the temporary M37 seams once no caller uses them:

- `IEvidenceRequestWriter`
- `EvidenceRequestWriter`
- legacy document-coupled commands in `LIAnsureProtect.Application.Quotes.Commands.ManageQuoteEvidenceRequests`
- evidence-document methods from `IQuoteRepository`
- evidence-document methods from `EfCoreQuoteRepository`
- legacy `QuoteEvidenceDocument`
- legacy `EvidenceDocumentScanStatus`
- legacy `QuoteEvidenceDocumentConfiguration`
- legacy `Application.Documents` scanner/storage contracts after their replacements are in Platform/Underwriting

Keep the allowed `LIAnsureProtect.Application -> Modules.Underwriting.Application` edge for now because
legacy referral queue/read code still consumes module Application readers. Do not loosen the architecture
test; update exact expected references only if a real project reference changes.

## 11. Testing Strategy

Unit tests:

- Move `QuoteEvidenceDocumentTests` to the Underwriting module namespace.
- Add or update handler tests for response upload, replacement upload, clean-only acceptance/review, and
  download blocking.
- Keep scanner tests focused on the local deterministic adapter and SHA-256 behavior.

Integration tests:

- Existing evidence document endpoints should keep passing with the same HTTP contracts.
- Add/adjust assertions proving document metadata now persists in `underwriting.quote_evidence_documents`.
- Pump the outbox dispatcher only when asserting notification/timeline effects; do not add sleeps.
- Keep the one PostgreSQL opt-in skip behavior unchanged.

Architecture tests:

- Prove modules still do not reference legacy layers or other modules.
- If moving `IDocumentStorageService` to Platform.Abstractions changes references, update the exact
  `ProjectReferenceBoundaryTests` `InlineData` rows. Do not widen the test.

Migration checks:

- `SubmissionDbContext` clean after dropping the legacy document table.
- `NotificationsDbContext` unchanged and clean.
- `UnderwritingDbContext` clean after creating the module document table.

## 12. Explicitly Deferred

- S3 storage adapter, presigned download URLs, KMS, retention/legal hold, and object lifecycle cleanup.
- OCR, embeddings, RAG, and autonomous evidence review.
- Download audit trails and malware analyst operations console.
- Full dispatcher decoupling / integration-event registry, still planned after the current carve.
- Final quote underwriting decision carve (`ApproveReferral` / `DeclineReferral` / `AdjustReferral` and
  `QuoteUnderwritingReview`), still separate because those methods mutate the Quote aggregate.

## 13. Roadmap Correction

The older production-transformation roadmap used placeholder milestone numbers for broad future modules.
The actual project took the safer path after M34: split Underwriting into smaller carve milestones instead
of moving every underwriting concern in one PR. M38 should therefore be Underwriting Evidence Documents,
not Claims. Claims remains later, after the existing underwriting/quoting seams are cleaner.
