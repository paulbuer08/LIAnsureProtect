# Milestone 28 - Evidence Document Security Screening Foundation Learnings

This document captures the planning and implementation notes for `Milestone 28 - Evidence Document Security Screening Foundation`.

## Starting Point

Branch:

```text
codex/milestone-28-evidence-document-security-screening-foundation
```

Starting commits:

```text
ab2e801 feat: add evidence document storage foundation
da28686 docs: close evidence document storage milestone
```

Milestone 27 added private local document storage for evidence responses. File bytes stayed outside PostgreSQL, safe metadata lived in `quote_evidence_documents`, and owners/underwriters downloaded files only through private API routes.

Milestone 28 builds on that by adding a security-screening trust state before those uploaded files are treated as safe to download or accept for underwriting review.

## Implemented Scope

- Added an Application-owned `IEvidenceDocumentScanner` boundary.
- Added an Infrastructure local deterministic scanner for development and tests.
- Persisted scan metadata on `quote_evidence_documents`:
  - `scan_status`
  - `scanner_provider_name`
  - `scan_result_code`
  - `scan_result_reason`
  - `scanned_at_utc`
  - `sha256`
- Defaulted evidence documents to `PendingScan`.
- Ran local scanning immediately after local storage writes.
- Marked files as `Clean`, `Rejected`, or `Failed`.
- Allowed owner and underwriter downloads only when the document is `Clean`.
- Returned a safe API conflict for authorized download attempts against pending, rejected, or failed documents.
- Blocked underwriter acceptance of evidence requests when any attached document is not clean.
- Added an owner replacement upload route for responded evidence requests with rejected or failed documents:

```text
POST /api/v1/evidence-requests/{evidenceRequestId}/documents
```

- Kept rejected/failed documents as audit evidence instead of deleting or replacing rows in place.
- Updated the owner evidence page and underwriting workbench to show scan status and hide download links until files are clean.

## Why This Is Realistic Specialty Insurance

Specialty insurance underwriting teams often collect sensitive supporting documents: MFA screenshots, EDR rollout exports, backup reports, incident response plans, prior loss details, and questionnaire clarifications. Those uploads are business evidence, but they are also untrusted user-supplied files.

A realistic document intake path should therefore separate two questions:

```text
Can this user upload evidence?
Can the system trust this file enough to let people download and review it?
```

Milestone 27 answered the first question with private storage, owner scoping, content-type and extension checks, file-size limits, generated storage keys, and private download routes.

Milestone 28 answers the second question with a narrow quarantine workflow:

```text
Uploaded document
  -> Stored privately
  -> PendingScan
  -> Scanner runs
  -> Clean, Rejected, or Failed
```

Only `Clean` documents are downloadable or acceptable as underwriting evidence. `Rejected` and `Failed` documents remain visible so the owner understands why replacement evidence is needed and the underwriter can see that the workflow preserved an audit trail.

## Scan Statuses

```text
PendingScan
```

The system has a document row, but it has not made a trust decision. This status is fail-closed: no download and no underwriter acceptance.

```text
Clean
```

The scanner made a clean trust decision. The document can be downloaded by an authorized owner or underwriter and can support evidence acceptance.

```text
Rejected
```

The scanner found the local deterministic test threat marker. In production this would map to a malware or policy violation result from an antivirus, malware scanning, or storage security provider.

```text
Failed
```

The scanner did not produce a clean decision. This is also fail-closed. A failed scan is not the same as a clean file.

## Local Scanner Behavior

The local scanner is deterministic and test-safe. It does not include real malware signatures and does not require external antivirus software.

The local test markers are:

```text
MALWARE-TEST-SIGNAL
```

Returns:

```text
scan_status: Rejected
scan_result_code: THREATS_FOUND
```

```text
SCAN-FAIL-TEST-SIGNAL
```

Returns:

```text
scan_status: Failed
scan_result_code: SCAN_FAILED
```

Any other file content returns:

```text
scan_status: Clean
scan_result_code: NO_THREATS_FOUND
```

The scanner also computes and stores the file SHA-256 hash. The hash gives future audit, duplicate detection, or provider-result correlation work a stable fingerprint without storing file bytes in PostgreSQL.

## Download And Acceptance Gates

Owner download route:

```text
GET /api/v1/evidence-requests/{evidenceRequestId}/documents/{documentId}/download
```

Underwriter download route:

```text
GET /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/documents/{documentId}/download
```

Both routes still enforce the existing authorization shape. The extra Milestone 28 rule is:

```text
document.scan_status must be Clean
```

If the caller is authorized but the document is not clean, the API returns a safe conflict response and does not stream bytes.

Underwriter evidence acceptance also checks attached document scan states:

```text
Only text-only evidence or evidence whose attached documents are all Clean can be accepted.
```

This keeps the underwriting workflow from accidentally treating rejected or failed files as usable evidence.

## Replacement Upload

Rejected and failed documents are not deleted in this milestone. Deletion, retention, and legal hold rules need their own explicit design.

Instead, the owner can append replacement evidence when a responded evidence request has at least one rejected or failed document:

```text
POST /api/v1/evidence-requests/{evidenceRequestId}/documents
```

The replacement files go through the same validation, storage, and scan process as the original response upload. The API result returns both the old rejected/failed documents and the new replacement documents so the UI can show the full audit trail.

## Out Of Scope

Milestone 28 intentionally does not add:

- production antivirus provisioning,
- AWS S3 bucket setup,
- AWS GuardDuty Malware Protection for S3,
- EventBridge scan-result ingestion,
- asynchronous document scanning workers,
- OCR/document extraction,
- embeddings or RAG,
- autonomous AI document review,
- legal hold or retention-policy automation,
- manual malware analyst console,
- public file URLs,
- full document management.

The milestone keeps the provider boundary and trust state local so a later milestone can replace the deterministic scanner with a production scanner without rewriting the Application use cases.

## Verification Notes

Focused backend checks used during implementation:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore --filter "QuoteEvidenceDocumentTests|QuoteEvidenceRequestTests"
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter "EvidenceDocumentEndpointTests|InfrastructureRegistrationProvidesPersistenceServices|PersistenceMigrationsCreatePgvectorExtensionAndSubmissionsTable"
```

Focused frontend checks used during implementation:

```powershell
& "C:\Users\Poy\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" ".\node_modules\vitest\vitest.mjs" run src\features\evidence\pages\EvidenceRequestsPage.test.tsx src\features\underwriting\pages\UnderwritingQuoteReferralsPage.test.tsx
```

Full verification passed with the standard project path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

Final verification results:

- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 52 passed and IntegrationTests 86 passed, 1 skipped PostgreSQL opt-in test.
- EF Core pending model check reported no pending model changes.
- Full frontend Vitest passed with 8 files and 29 tests.
- Full local CI passed with artifact `TestResults\local-ci-20260623-160248.zip`.
