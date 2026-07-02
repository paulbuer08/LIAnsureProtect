# Milestone 38 - Underwriting Evidence Documents - Learning Notes

## Goal

Milestone 38 completed the evidence-document part of the Underwriting carve.
Milestone 37 had already moved evidence requests, reviews, and evidence events into the
Underwriting module, but document metadata and document-coupled workflows still sat in the legacy
Application/Quotes path behind a temporary writer seam.

M38 moved those document responsibilities into the module:

- generic private document storage contracts moved to `Platform.Abstractions`;
- evidence scanning became an Underwriting module port;
- `QuoteEvidenceDocument` and `EvidenceDocumentScanStatus` moved into Underwriting Domain;
- document metadata moved from `public.quote_evidence_documents` to
  `underwriting.quote_evidence_documents`;
- owner response, replacement upload, owner/underwriter download, accept, review-decision, and
  document-aware owner-list reads became Underwriting Application workflows;
- the temporary M37 `IEvidenceRequestWriter` seam and legacy document methods on `IQuoteRepository`
  were deleted.

The public HTTP routes and React contract stayed stable.

## The boundary split that mattered

Not every "document" concept belongs in the same layer.

Generic storage is platform infrastructure:

```text
IDocumentStorageService
DocumentStorageUpload
StoredDocumentResult
StoredDocumentDownload
```

These contracts now live in:

```text
src/Platform/LIAnsureProtect.Platform.Abstractions/Documents/
```

That is the right home because local filesystem storage and future S3 storage are platform adapters.
They store bytes; they do not decide whether a cyber evidence file is acceptable underwriting proof.

Evidence scanning is different. It produces underwriting trust state:

```text
PendingScan
Clean
Rejected
Failed
```

That is business meaning inside the evidence workflow, so the scanner port moved into:

```text
src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Evidence/Documents/
```

The local deterministic scanner moved into Underwriting Infrastructure as the module's local adapter.
This keeps Platform generic and keeps underwriting-specific document trust logic in the Underwriting
module.

## Why the handlers could move only after the ports moved

The document handlers needed both:

- private byte storage through `IDocumentStorageService`;
- scan results through `IEvidenceDocumentScanner`;
- evidence request state through module repositories;
- document metadata persistence through `UnderwritingDbContext`.

If the handlers had moved first, the Underwriting module would have needed to reference legacy
Application document ports, which violates the module boundary rule. The safe order was:

```text
1. Move storage contract to Platform.Abstractions.
2. Move scanner contract and local scanner into Underwriting.
3. Move document aggregate and persistence into Underwriting.
4. Move document workflows into Underwriting.
5. Delete the temporary legacy seams.
```

That order kept every commit buildable and avoided weakening the architecture test.

## The table move

Document metadata moved by drop-and-recreate, matching the current no-production-data pattern used
by the module carves:

```text
SubmissionDbContext
  -> DropEvidenceDocuments
  -> drops public.quote_evidence_documents

UnderwritingDbContext
  -> CreateEvidenceDocuments
  -> creates underwriting.quote_evidence_documents
```

The new table still stores metadata only:

- evidence request id;
- quote id;
- submission id;
- owner user id;
- original file name;
- content type;
- size;
- opaque storage key;
- uploader and upload timestamp;
- scan status/provider/result/reason/timestamp;
- SHA-256 hash.

File bytes still stay outside PostgreSQL behind the storage port.

There are intentionally no cross-schema foreign keys back to `quotes`, `submissions`, or evidence
requests. The module stores scalar correlation ids and owns its own schema.

## Transaction boundary after M38

M37 had a split transaction shape:

```text
legacy handler stores/scans document metadata
  -> module writer updates evidence request/review state
```

M38 restores one module database transaction for request/review state, document metadata, review audit
rows, and module outbox capture:

```text
owner responds with documents
  -> validate upload set
  -> load module QuoteEvidenceRequest
  -> store file bytes through IDocumentStorageService
  -> reopen stored bytes
  -> scan through IEvidenceDocumentScanner
  -> add QuoteEvidenceDocument rows to UnderwritingDbContext
  -> update QuoteEvidenceRequest
  -> SaveChangesAsync on UnderwritingDbContext
  -> ModuleDbContext captures evidence domain event into underwriting.outbox_messages
```

One caveat remains: bytes are stored before the database save. If the database save fails after the
object is written, a local/S3 object can be orphaned. That is not new; it existed in the legacy path
too. Object cleanup, S3 lifecycle policies, retention, and durable download audit belong to the later
documents/S3 hardening milestones.

## Clean-only gates stayed fail-closed

The carve did not loosen document trust rules:

- only `Clean` documents are downloadable;
- `Rejected`, `Failed`, and `PendingScan` documents are visible as metadata but do not stream bytes;
- underwriters cannot accept or satisfy evidence when any attached document is not clean;
- replacement uploads are allowed only after a response has at least one rejected or failed document;
- replacement uploads append new rows and keep rejected/failed originals for audit.

Text-only evidence responses remain valid. With no attached documents, there is no unsafe document to
block.

## The temporary M37 seam is gone

M37 needed `IEvidenceRequestWriter` because legacy document handlers could not update a module-owned
aggregate directly. Once M38 moved the document handlers into the module, that seam became harmful:
it preserved a cross-boundary command path that no caller needed.

The cleanup removed:

- `IEvidenceRequestWriter`;
- `EvidenceRequestWriter`;
- legacy document-coupled commands in
  `LIAnsureProtect.Application.Quotes.Commands.ManageQuoteEvidenceRequests`;
- evidence document methods from `IQuoteRepository`;
- evidence document methods from `EfCoreQuoteRepository`;
- the old `Application.Documents` scanner/storage contracts.

`IQuoteRepository` is quote-focused again. Underwriting document workflows now use
`IEvidenceRequestRepository` and `IEvidenceDocumentRepository` inside the module.

## Tests that mattered

The most valuable regression tests were the endpoint tests because the public routes stayed stable
while the ownership moved underneath them.

Coverage preserved or added:

- owner upload of five documents still returns safe document metadata and never leaks storage keys;
- owner list now returns document metadata from the module-owned document table;
- rejected and failed scan metadata stays visible but not downloadable;
- owner and underwriter download routes remain private and clean-only;
- accept/review decisions remain blocked by unclean documents;
- replacement uploads append new clean rows while preserving rejected originals;
- referral/evidence integration tests still pump the outbox dispatcher before timeline or notification
  assertions.

No assertion was removed to make the suite green.

## Verification

Fresh verification for M38 closeout:

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
- full solution tests passed with UnitTests 62 and IntegrationTests 124, with the one PostgreSQL opt-in
  test skipped by design;
- all three EF Core pending-model checks reported no pending changes;
- full local CI passed against fresh Docker PostgreSQL, all three context migration sets, backend
  tests, frontend install/build/lint/tests, artifact creation, and Docker cleanup;
- local CI artifact: `TestResults\local-ci-20260702-092510.zip`.

## What is next

Milestone 39 should address the next hard boundary: the Quoting decision boundary.

The final approve/decline/adjust commands still mutate the `Quote` aggregate and write
`QuoteUnderwritingReview`. That should not be forced into Underwriting just because the referral and
evidence operations are there now. Underwriting owns operational work and evidence; Quoting still owns
quote terms and final quote decision state until the next carve defines a cleaner boundary.

The dispatcher integration-event registry/decoupling remains a near-term follow-up after the current
Underwriting/Quoting seams are cleaner.
