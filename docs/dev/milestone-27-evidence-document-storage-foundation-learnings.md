# Milestone 27 - Evidence Document Storage Foundation Learnings

This document starts the planning and learning notes for `Milestone 27 - Evidence Document Storage Foundation`.

## Starting Point

Branch:

```text
codex/milestone-27-evidence-document-storage-foundation
```

Starting commits:

```text
1f790e0 feat: add evidence request notification follow-up foundation
5ca941d docs: close evidence request notification follow-up milestone
```

Milestone 26 made evidence requests operational by adding local outbox-backed notifications, a manual underwriter follow-up reminder action, timeline audit evidence, and due/overdue indicators in the underwriting workbench and owner evidence page.

## Recommended Scope

The Milestone 27 target is the first real evidence document storage slice.

Implemented scope:

- Added an Application-owned document storage boundary for evidence attachments.
- Added a local filesystem storage implementation for development and tests before production S3.
- Replaced evidence response attachment metadata placeholders with a multipart upload path.
- Supported up to five evidence files per response.
- Stored safe file metadata in a separate PostgreSQL table named `quote_evidence_documents`.
- Added private owner and underwriter download/read access behind existing authorization rules.
- Recorded audit-friendly storage metadata: evidence request id, quote id, submission id, owner user id, original file name, content type, size, storage key, uploaded by user id, and uploaded timestamp.
- Kept file bytes out of PostgreSQL.
- Kept the first storage path local and provider-shaped so S3 can replace it later without rewriting Application use cases.

## Why This Is Realistic Specialty Insurance

Cyber underwriters usually need real supporting documents, not only text responses. Examples include MFA screenshots, EDR deployment reports, backup test evidence, incident response plans, prior loss details, and questionnaire clarifications.

Milestone 25 created the evidence request workflow. Milestone 26 made the workflow operational with notifications and follow-ups. Milestone 27 can now make evidence responses more realistic by giving the app a controlled way to store and retrieve uploaded evidence documents.

The five-file limit is intentional. Three files was too conservative for real cyber underwriting because a single response can reasonably include a PDF attestation, a screenshot, a control report, a spreadsheet export, and a short supporting note. Ten files would be possible later, but it starts to pull the milestone toward full document management: bulk upload UI, partial failure handling, cleanup if file 7 of 10 fails, larger request limits, and more operational edge cases. Five files gives realistic underwriting behavior while keeping this milestone focused on the storage foundation.

## Out Of Scope

Milestone 27 should not become:

- production S3 bucket provisioning,
- public file URLs,
- virus scanning,
- OCR/document extraction,
- embeddings or RAG,
- autonomous AI document review,
- legal hold or retention-policy automation,
- full document management,
- multi-party messaging threads,
- notification inboxes,
- scheduled reminder automation.

The goal is the storage boundary and local proof of behavior, not the full production document platform.

## Key Design Decisions

- The first slice supports a bounded list of up to five files per evidence response.
- Document metadata lives in `quote_evidence_documents`, not directly on `quote_evidence_requests`.
- `quote_evidence_requests` remains the evidence workflow row: requested, responded, accepted, cancelled, due, overdue, and review state.
- `quote_evidence_documents` answers the storage/audit question: which files were uploaded, by whom, when, under which request, and where the bytes are stored.
- The local storage root is configured through `DocumentStorage:LocalRootPath`. If not configured, the Infrastructure provider falls back to an app-local `App_Data` path.
- Downloads are streamed through private API routes. The first slice does not return public URLs or presigned URL shapes.
- Owner download access uses owner scoping. A different owner receives `404 Not Found` rather than learning that another owner's document exists.
- Underwriter/admin download access uses the existing `Quotes.Underwrite` policy under the underwriting referral route.
- The API validates up to five files, supported content types/extensions, non-empty files, a maximum per-file size, a maximum total response size, and uploaded names that do not contain path information.
- The storage key is generated server-side by the storage provider. Clients provide the original file name only for display/audit metadata.

## Storage Flow

```text
Customer/Broker owner
  -> POST /api/v1/evidence-requests/{evidenceRequestId}/respond
     multipart/form-data
  -> EvidenceRequestsController
  -> RespondToQuoteEvidenceRequestCommand
  -> IDocumentStorageService.StoreAsync(...)
  -> LocalDocumentStorageService writes bytes to local disk
  -> quote_evidence_documents stores safe metadata and storage key
  -> quote_evidence_requests stores response text and response status
```

Owner download:

```text
GET /api/v1/evidence-requests/{evidenceRequestId}/documents/{documentId}/download
```

Underwriter download:

```text
GET /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/documents/{documentId}/download
```

The API streams the file bytes back with the original file name and content type. The local storage key is never returned to the frontend.

## Metadata Table

```text
quote_evidence_documents
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
```

Why this is separate from `quote_evidence_requests`:

- One evidence response can now have more than one document.
- Document storage metadata can evolve later without bloating the workflow row.
- A future S3 implementation can keep the same Application use case and replace the local storage provider.
- Later download audit, malware scan state, retention metadata, or OCR/extraction status can attach to documents without changing the evidence request lifecycle.

## Likely File Areas

Backend:

- `src/LIAnsureProtect.Application/Documents`
- `src/LIAnsureProtect.Application/Quotes`
- `src/LIAnsureProtect.Infrastructure/Documents`
- `src/LIAnsureProtect.Infrastructure/Persistence`
- `src/LIAnsureProtect.Api/Controllers`
- `tests/LIAnsureProtect.UnitTests`
- `tests/LIAnsureProtect.IntegrationTests`

Frontend:

- `src/LIAnsureProtect.Web/src/features/evidence`
- `src/LIAnsureProtect.Web/src/features/underwriting`

Docs:

- `README.md`
- `CHANGELOG.md`
- `docs/project-status.md`
- `docs/architecture/overview.md`
- `docs/dev/pattern-roadmap-after-milestone-11.md`
- this learning note

## Starter Verification Path

Use the standard local verification path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

## Verification Notes

Focused backend checks passed:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter EvidenceDocumentEndpointTests
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter PersistenceMigrationsCreatePgvectorExtensionAndSubmissionsTable
```

Focused frontend checks passed with the bundled Codex Node runtime:

```powershell
& "C:\Users\Poy\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" ".\node_modules\vitest\vitest.mjs" run src\features\evidence\pages\EvidenceRequestsPage.test.tsx src\features\underwriting\pages\UnderwritingQuoteReferralsPage.test.tsx
```

Full verification passed:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
& "C:\Program Files\PowerShell\7\pwsh.exe" -Command ".\scripts\run-local-ci.ps1 -RunFrontendInstall:`$false"
```

Results:

- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 48 passed and IntegrationTests 81 passed, 1 skipped PostgreSQL opt-in test.
- EF Core pending model check reported no pending model changes.
- Full local CI passed with Docker-backed PostgreSQL, all committed migrations applied including `20260622235023_AddQuoteEvidenceDocuments`, backend tests, Docker Compose config validation, frontend build, frontend lint, frontend tests, artifact creation, and Docker cleanup.
- Final local CI artifact: `TestResults\local-ci-20260623-080954.zip`.

One testing note: running two `dotnet test` commands in parallel can lock the API build output under `src\LIAnsureProtect.Api\obj\Debug\net10.0`. When that happened during focused verification, rerunning the affected test by itself passed.
