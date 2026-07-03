# Claims Milestone 3 - Claim Documents — Design

> Branch-local doc (`docs/claims/` policy — see `claims-status.md`).

## What this milestone builds

Claimants attach **supporting documents** to a claim (proof of loss, invoices, forensic reports);
every upload passes the established **quarantine scan** before anyone can download it; adjusters
download only **clean** documents; a rejected/failed scan lets the claimant upload replacements.
This is the M27/M28/M38 evidence-document pattern applied to the Claims context.

> **Analogy (same as the evidence mailroom):** documents arrive through a slot, are X-rayed in a
> back room before anyone touches them, and only clean envelopes reach the adjuster's desk.
> A suspicious envelope is kept — sealed — for the record, and the sender is asked to resend.

## Scope

In: `ClaimDocument` aggregate-child + `claims.claim_documents` table, upload on file-a-claim's
detail path (`POST /api/v1/claims/{id}/documents`, multipart), clean-only downloads for both the
owner and the adjuster, replacement uploads after a rejected/failed scan, the module's own
scanner port + local deterministic adapter, `ClaimDocumentUploaded` domain event.

Out: S3-triggered scanning (arrives platform-wide later), document notifications (CM6), UI (CM7).

## Reused seams vs module-owned pieces

| Piece | Source |
|---|---|
| Byte storage | **Reused as-is**: `IDocumentStorageService` (Platform kernel — filesystem or S3+SSE-KMS by `Platform:Profile`; storage keys never leak) |
| Scanner | **Pattern copied, port module-owned**: `IClaimDocumentScanner` in Claims Application + `LocalDeterministicClaimDocumentScanner` adapter (the Underwriting scanner port is module-internal and modules must not reference each other; the deterministic marker behavior and SHA-256 are identical) |
| Upload governance | Same rules class shape (`ClaimDocumentUploadRules`): max 5 files per upload, 10 MB per file, 50 MB total, content-type/extension allow-list, no path info in names |
| Fail-closed gates | Same: only `Clean` documents are downloadable; `Rejected`/`Failed` stay visible for audit but never downloadable; replacements append, never delete |

## Domain

`ClaimDocument` (child of the claim, `claims.claim_documents`): id, claimId, kind
(**ProofOfLoss, Invoice, ForensicReport, Correspondence, Other**), original file name, content
type, size, storage key, uploaded-by/at, scan status (PendingScan/Clean/Rejected/Failed), scanner
metadata (provider, code, reason), SHA-256, `IsDownloadAvailable => ScanStatus == Clean`.

On the `Claim` aggregate: `AddDocument(...)` (open claims only — no uploads after a decision),
timeline entry `DocumentUploaded`, `ClaimDocumentUploadedDomainEvent` (per upload batch — one
event per document keeps payloads small and mapping simple). Replacement uploads are the same
`AddDocument` path — allowed whenever at least one existing document is Rejected/Failed **or**
the claim is open (claim documents are voluntary attachments, unlike evidence responses that
answer a specific request — so the "replacement only after rejection" restriction applies to
nothing here; any open claim accepts more documents; recorded as a deliberate loosening of the
evidence shape, with the audit story unchanged: nothing is ever deleted).

## API surface

| Endpoint | Policy | Effect |
|---|---|---|
| `POST /api/v1/claims/{claimId}/documents` (multipart, `kind` + files) | `Claims.Respond` (owner-scoped) | store → scan → persist metadata; 201 with per-document scan outcomes |
| `GET /api/v1/claims/{claimId}/documents/{documentId}/download` | `Claims.Read` (owner-scoped) | clean-only stream; 409 if not clean |
| `GET /api/v1/claims/adjudication/{claimId}/documents/{documentId}/download` | `Claims.Adjudicate` | clean-only stream; 409 if not clean |
| Documents listed on both detail endpoints | existing | name/kind/size/scan status/uploaded-at (storage key never exposed) |

## Testing plan (TDD)

1. **Domain:** document factory validation, scan-result recording (incl. SHA-256 shape),
   `AddDocument` guards (closed claim rejected), timeline + event, download-availability flag.
2. **Application:** upload handler (governance rejections: too many files, oversize, bad
   content type, path in name), store→scan→persist order, owner scoping (404), download handlers
   (owner + adjudication) with the clean-only 409 gate.
3. **Endpoints:** multipart upload → 201 + PendingScan→Clean metadata; rejected marker file →
   Rejected status, download → 409, replacement upload accepted alongside; owner/adjuster
   download round-trips byte-for-byte; foreign owner 404; storage key absent from every payload;
   migration-script facts for `claims.claim_documents`.
