# Claims Milestone 3 - Claim Documents — Learnings

> Companion to [the design doc](cm3-claim-documents-design.md). Branch-local.

## What shipped

- `ClaimDocument` children on the `Claim` aggregate (`claims.claim_documents`): kind
  (ProofOfLoss/Invoice/ForensicReport/Correspondence/Other), file metadata, private storage key,
  scan status + scanner metadata + SHA-256, `IsDownloadAvailable` only when `Clean`.
- `POST /api/v1/claims/{id}/documents` (multipart, owner-scoped, `Claims.Respond`): store via
  the **shared Platform `IDocumentStorageService`** → quarantine-scan via the **module-owned**
  `IClaimDocumentScanner` (local deterministic adapter, same test markers as evidence) →
  persist with outcome; per-file results returned; `ClaimDocumentUploadedDomainEvent` per file.
- Clean-only downloads on both surfaces (`Claims.Read` owner route + `Claims.Adjudicate`
  adjudication route) — Rejected/Failed → 409, fail-closed for every role.
- Replacement uploads simply append; rejected originals stay visible for audit (nothing is ever
  deleted). Upload governance identical to evidence: 5 files, 10 MB each, 50 MB total,
  content-type/extension allow-list, no path info.

## Decisions and why

**Scanner port is module-owned, storage port is shared.** `IDocumentStorageService` lives in the
Platform kernel (M38 promoted it) so Claims reuses it as-is — that is why the S3 profile switch
costs Claims nothing. The evidence scanner port however is Underwriting-Application-internal;
modules must not reference each other, so Claims declares its own `IClaimDocumentScanner` with
the same shape and a deterministic local adapter with the **same test markers**
(`MALWARE-TEST-SIGNAL`, `SCAN-FAIL-TEST-SIGNAL`) so one manual-testing vocabulary works
everywhere. When a real platform-wide scanning service arrives (S3-triggered Lambda), both
module ports become thin adapters over it.

**Scan the stored bytes, not the request stream.** The verdict and SHA-256 must describe exactly
what an adjuster could later download — so the workflow stores first, re-opens the stored object,
and scans that (same order as M28).

**No "replacement only after rejection" gate.** Evidence documents answer a specific request, so
replacements were gated on a failed scan. Claim documents are voluntary attachments to an open
claim — any open claim accepts more documents; the fail-closed download gate is what actually
protects the adjuster. Recorded as a deliberate loosening of the copied shape.

**Documents rejected after a decision.** `AddDocument` reuses the CM2 `EnsureOpenForAdjusting`
guard: once a claim is Accepted/Denied/Closed the file is frozen — late paperwork needs a
reopened claim (future scope), not a mutable closed file.

## Gotchas hit

- **The zero-warning gate failed the build on CA1822** (a test helper that could be static) —
  and because the test project failed to compile, a `--no-build` test run silently reported the
  *previous* DLL's results. Lesson: after adding tests, confirm the test count went **up** (or
  grep the build for errors) before trusting a green line; the "Passed!" was real but stale.
- `MultipartFormDataContent` field names must match the `[FromForm]` record's property names
  (`kind`, `attachments`) — the binder is case-insensitive but name-sensitive.

## Verification

- Full backend suite: **142 unit + 202 integration passed**, 4 skipped (opt-in), zero warnings.
- New coverage: 10 domain tests (quarantine start, gates, SHA validation, closed-claim guard,
  append-not-delete), 10 handler tests (governance matrix, ownership, store→scan→persist,
  download gates both audiences), 8 endpoint tests (clean round trip owner+adjuster byte-for-byte,
  rejected/failed lockouts, replacement append, 404/400/403, storage-key never serialized),
  1 migration fact.
- `AddClaimDocuments` migration applied to local Docker Postgres (additive).

## Intentionally not built yet

Document notifications (CM6), S3-triggered scanning (platform milestone), pairing uploads with
information-request answers in one call (the UI can sequence the two existing endpoints — CM7
decides whether that composition is needed), Valet-Key presigned downloads (M47 platform-wide).
