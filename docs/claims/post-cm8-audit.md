# Post-CM8 Audit — Full Re-Scrutiny Of The Claims Branch

> The Claims-branch equivalent of the Post-M41/Post-M44 audits: after CM8 closed, the entire
> `feat/claims-context` diff (~17,300 lines vs main) was re-reviewed adversarially for
> correctness bugs, security holes, performance traps, and best-practice drift. Three findings
> were **fixed** on this branch; five are **recorded** with reasons (all inherited patterns or
> config-time verifications), added to the final-merge checklist where action is due.

## Fixed in this audit

### 1. Cartesian explosion on the claim include-graphs (performance, would degrade with data)

`EfClaimRepository.GetByIdForUpdateAsync` and the two detail readers loaded up to **six child
collections through single-query `Include`s** — EF Core joins these into one SQL result whose
row count is the *product* of the collection sizes (a claim with 20 timeline entries, 5 notes,
5 requests, 5 documents, 10 reserve changes, and 3 decisions materializes 75,000 rows to
hydrate ~48 entities). Fixed with `AsSplitQuery()` on all three (one query per collection).
The full existing endpoint suite passed unchanged — behavior-preserving by proof.

### 2. The adjudication queue materialized every information request (performance)

`ListQueueAsync` `Include`d all information requests for every open claim just to compute the
open-question badge count. Replaced with a pure SQL projection
(`claim.InformationRequests.Count(r => !r.IsAnswered)` translates to a subquery), which also
stops loading full aggregate rows for the queue. Same shape the owner list already used.

### 3. The reserve was permanently frozen after a decision (domain modeling hole)

`SetReserve` is guarded by `EnsureOpenForAdjusting`, so once a claim was decided **nobody could
ever release the outstanding reserve** — closed files kept money earmarked forever, which is
exactly what a reserve must not do. Fix (TDD, watched fail first): `Close` now automatically
releases any outstanding reserve to zero with a normal audited `ClaimReserveChange` row
("Reserve released on claim closure.") and a timeline entry; the `Closed` decision audit row
still snapshots the **pre-release** reserve so the ledger shows what was outstanding when the
file was finished. Zero-reserve closes add no noise row.

## Recorded, not fixed (with reasons)

1. **Document download links carry no bearer token.** ✅ **RESOLVED (2026-07-04).** Fixed
   cross-feature as recommended: the shared `downloadDocumentWithToken` helper
   (`lib/documentDownload.ts`) fetches with the bearer token and saves the bytes as a blob.
   Evidence + underwriting pages were fixed on main (PR #52); the claims pages adopted the same
   helper on this branch immediately after syncing. M47 presigned Valet-Key URLs remain the
   long-term replacement.
2. **Kestrel's default 30 MB body limit vs the 50 MB upload governance.** ✅ **RESOLVED
   (2026-07-04, PR #52 on main).** Kestrel's `MaxRequestBodySize` is now 60 MB (headroom above
   the 50 MB rule), set in the API host with a comment tying it to
   `EvidenceDocumentUploadRules`.
3. **Orphaned blob on a rejected upload.** If a claimant uploads to a claim that was decided a
   moment earlier, the bytes are stored before `AddDocument` throws — an unreferenced file
   remains in storage (no metadata row, so it is unreachable, not a security issue). Same
   ordering as the evidence workflow (M27); a storage janitor would fix both. Recorded only.
4. **Frontend role guard depends on the ID token carrying the namespaced roles claim**
   (`https://liansureprotect.local/roles`). The API validates it on access tokens; the Auth0
   Action must add it to ID tokens too or `RequireRole` blocks legitimate users. First frontend
   role dependency in the repo — tenant verification added to the final-merge checklist.
5. **Claim documents live under the `evidence-documents/` storage prefix** — the platform
   adapter hardcodes the prefix in the key it generates. Cosmetic (keys are opaque GUIDs);
   renaming the prefix would orphan existing evidence files. Recorded only.

## Practices re-verified clean (no findings)

- **AuthZ:** every endpoint policy-guarded; ownership checks return 404 (no existence leak);
  reserve/history never serialized to claimants (endpoint-tested); storage keys never leave the
  server; adjudication reads role-gated.
- **Injection/traversal:** no raw SQL anywhere (EF parameterized); storage keys are
  server-generated GUIDs and `ResolveStoragePath` re-verifies containment; upload filenames
  must be path-free and extension/content-type allow-listed; scans hash the stored bytes.
- **Concurrency:** the `Version` token covers every mutation path; the race is proven at the
  persistence level; 409s surface with refetch UX.
- **Domain invariants:** every state flip goes through a guard method with an audit artifact
  (timeline/decision/reserve rows); no public setters; factories only; partial-mutation
  ordering re-checked in `Accept`/`Deny`/`Close` (all guards run before any assignment).
- **Async/eventing conventions:** cancellation tokens threaded end-to-end; no blocking-on-async,
  no `Task.Run`, no `async void`; commands synchronous in the core, cross-context effects via
  the outbox; consumers idempotent on the source message id.
- **Zero-warning gate** held across all nine PRs; module-boundary ratchet green; culture-fixed
  money formatting in audit text and notification attributes; generic test values only.

## Verification

Full backend suite after fixes: **181 unit + 237 integration passed** (2 new domain tests for
the reserve release; every pre-existing test unchanged and green — the strongest available
proof that the two query rewrites preserved behavior). Frontend untouched.
