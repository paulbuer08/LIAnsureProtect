# Milestone 38 - Underwriting Evidence Documents Implementation Plan

> Branch: `feat/milestone-38-underwriting-evidence-documents`
>
> Source design: `docs/dev/milestone-38-underwriting-evidence-documents-design.md`
>
> Execute task-by-task, in order. Make exactly the task's changes, run its verification commands, ensure
> `dotnet build LIAnsureProtect.slnx --no-restore` reports `0 Warning(s), 0 Error(s)`, then commit that
> task before starting the next task.

## Conventions

- Read first: `AGENTS.md`, `docs/project-status.md`, this plan, the M38 design doc,
  `docs/dev/async-and-eventing-conventions.md`, and the M35-M37 learning notes.
- Do not create or switch branches; stay on `feat/milestone-38-underwriting-evidence-documents`.
- No AI attribution in commits or PRs.
- Do not weaken tests. If a test needs eventual consistency, pump the outbox dispatcher, then assert.
- Do not loosen module-boundary tests. Only update exact expected references when a project reference
  really changes.
- Keep all three EF Core contexts checked with these exact commands before closeout:

```powershell
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```

## Phase A - Legalize the Document Ports

### Task A1 - Move generic document storage contracts to Platform.Abstractions

Goal: let modules depend on private document storage without referencing legacy Application.

Changes:

- Add `src/Platform/LIAnsureProtect.Platform.Abstractions/Documents/DocumentStorageContracts.cs`.
- Move these contracts from `src/LIAnsureProtect.Application/Documents/DocumentStorageContracts.cs`:
  - `IDocumentStorageService`
  - `DocumentStorageUpload`
  - `StoredDocumentResult`
  - `StoredDocumentDownload`
- Update all using statements from `LIAnsureProtect.Application.Documents` to
  `LIAnsureProtect.Platform.Abstractions.Documents` where they refer to storage.
- Delete the old storage contract file if it becomes empty.
- Update `ProjectReferenceBoundaryTests` only if exact project references changed.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter ProjectReferenceBoundaryTests
```

Commit:

```text
refactor: move document storage port to platform
```

### Task A2 - Move evidence document scanner port into Underwriting

Goal: make evidence scanning a module-owned business port before document handlers move.

Changes:

- Add `src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Evidence/Documents/IEvidenceDocumentScanner.cs`.
- Move these contracts from `src/LIAnsureProtect.Application/Documents/EvidenceDocumentScannerContracts.cs`:
  - `IEvidenceDocumentScanner`
  - `EvidenceDocumentScanRequest`
  - `EvidenceDocumentScanResult`
- For now the scan result can still reference the legacy `EvidenceDocumentScanStatus`; Task B1 removes
  that temporary dependency by moving the enum with the aggregate.
- Move `LocalDeterministicEvidenceDocumentScanner` from legacy Infrastructure into
  `src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Evidence/Documents/`.
- Register the scanner in `AddUnderwritingModule(...)`, not `AddInfrastructure(...)`.
- Remove the old scanner contract file if it becomes empty.
- Update any scanner tests/namespaces.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter ProjectReferenceBoundaryTests
```

Commit:

```text
refactor: move evidence document scanner port
```

## Phase B - Move Document Metadata Into Underwriting

### Task B1 - Move the evidence document aggregate and enum

Goal: make document metadata a module domain concept.

Changes:

- Move `QuoteEvidenceDocument` from `src/LIAnsureProtect.Domain/Quotes/` to
  `src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Evidence/Documents/`.
- Move `EvidenceDocumentScanStatus` beside it.
- Update namespaces and using statements.
- Update `IEvidenceDocumentScanner` so scan results use the module enum.
- Move `QuoteEvidenceDocumentTests` to the module namespace/folder.
- Ensure no module references legacy `LIAnsureProtect.Domain`.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter "QuoteEvidenceDocumentTests|ProjectReferenceBoundaryTests"
```

Commit:

```text
refactor: move evidence document aggregate
```

### Task B2 - Add module persistence for evidence documents

Goal: persist document metadata in the `underwriting` schema.

Changes:

- Add `DbSet<QuoteEvidenceDocument>` to `UnderwritingDbContext`.
- Move/adapt `QuoteEvidenceDocumentConfiguration` into Underwriting Infrastructure.
- Keep indexes and column names equivalent to the legacy table.
- Remove cross-schema FKs to `quotes` and `submissions`; ids stay scalar correlation ids.
- Add an `IEvidenceDocumentRepository` in Underwriting Application with the methods document handlers need:
  - add documents
  - list by request ids
  - get owner document
  - get underwriting document
- Implement it in Underwriting Infrastructure.
- Register it in `AddUnderwritingModule(...)`.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter ProjectReferenceBoundaryTests
```

Commit:

```text
feat: add underwriting evidence document persistence
```

### Task B3 - Add EF migrations for the document table move

Goal: create the module document table and drop the legacy table.

Changes:

- Add an `UnderwritingDbContext` migration that creates `underwriting.quote_evidence_documents`.
- Add a `SubmissionDbContext` migration that drops legacy `public.quote_evidence_documents`.
- Update migration tests/snapshots as needed.
- Do not manually edit snapshots except through EF migration generation unless a migration needs a small
  reviewed correction.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter "Migrations|PersistenceMigrations"
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```

Commit:

```text
feat: move evidence document table to underwriting
```

## Phase C - Move Document Workflows Into Underwriting

### Task C1 - Move owner response and replacement upload handlers

Goal: keep upload behavior stable while saving request state and document metadata in one module context.

Changes:

- Move `RespondToQuoteEvidenceRequestCommand` and `UploadReplacementEvidenceDocumentsCommand` into
  Underwriting Application.
- Move `EvidenceDocumentUpload`, upload validation, storage+scan workflow, and result mapping into the
  new module document area.
- Replace `IEvidenceRequestsReader + IEvidenceRequestWriter` orchestration with tracked
  `IEvidenceRequestRepository` loads and one `SaveChangesAsync`.
- Use `IEvidenceDocumentRepository` for document rows.
- Keep storage bytes behind `IDocumentStorageService` from Platform.Abstractions.
- Update `EvidenceRequestsController` namespaces only; route behavior should not change.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter "Evidence|ProjectReferenceBoundaryTests"
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter EvidenceDocumentEndpointTests
```

Commit:

```text
feat: move evidence document uploads to underwriting
```

### Task C2 - Move accept, review, and download handlers

Goal: move all remaining document-coupled evidence workflows into the module.

Changes:

- Move `AcceptQuoteEvidenceRequestCommand`.
- Move `RecordQuoteEvidenceReviewDecisionCommand`.
- Move `DownloadOwnerEvidenceDocumentQuery`.
- Move `DownloadUnderwritingEvidenceDocumentQuery`.
- Move `EvidenceDocumentDownloadResult`.
- Keep clean-only gates exactly equivalent:
  - blocked download when scan status is not clean
  - blocked accept/review when any attached document is not clean
- Update `EvidenceRequestsController` and `UnderwritingQuoteReferralsController` namespaces only.
- Ensure returned document DTOs include the same fields as before.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter "EvidenceDocumentEndpointTests|UnderwritingReferralEndpointTests"
```

Commit:

```text
feat: move evidence document review gates
```

### Task C3 - Fold document metadata into module owner reads

Goal: remove the last legacy document read from owner evidence listing.

Changes:

- Extend the module reader/repository so `ListOwnerEvidenceRequestsQuery` returns document metadata.
- Remove the legacy duplicate `ListOwnerEvidenceRequestsQuery` and duplicate result records if no callers
  remain.
- Keep owner list response shape stable for the frontend.
- Update frontend TypeScript only if import paths or generated shapes changed; do not change UI behavior
  unless tests show a real contract mismatch.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter EvidenceDocumentEndpointTests
```

Commit:

```text
feat: include underwriting evidence documents in owner reads
```

## Phase D - Delete Legacy Document Carve Remnants

### Task D1 - Remove legacy evidence document repository surface

Goal: make `IQuoteRepository` quote-focused again.

Changes:

- Remove evidence document methods from `IQuoteRepository`.
- Remove corresponding methods from `EfCoreQuoteRepository`.
- Remove legacy `SubmissionDbContext.QuoteEvidenceDocuments`.
- Delete legacy document EF configuration.
- Delete legacy document domain files if Task B1 left wrappers or aliases.
- Delete `IEvidenceRequestWriter` and `EvidenceRequestWriter` once no callers remain.
- Verify no `QuoteEvidenceDocument` references remain outside Underwriting module/tests except migration
  snapshots for historical migrations.

Verification:

```powershell
rg "QuoteEvidenceDocument|IEvidenceRequestWriter|EvidenceRequestWriter|ListEvidenceDocumentsForRequestsAsync|AddEvidenceDocumentsAsync" src tests
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter ProjectReferenceBoundaryTests
```

Commit:

```text
refactor: remove legacy evidence document seams
```

### Task D2 - Regression test the full evidence flow

Goal: prove the carve did not weaken behavior.

Changes:

- Update tests only where namespaces, DbContext ownership, or eventual consistency require it.
- Add assertions that module document rows are used where useful.
- Do not delete existing behavior assertions.
- Pump the outbox dispatcher for notification/timeline effects.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter "EvidenceDocumentEndpointTests|UnderwritingReferralEndpointTests|NotificationInboxEndpointTests"
```

Commit:

```text
test: cover underwriting evidence documents carve
```

## Phase E - Closeout

### Task E1 - Full verification

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```

If Docker is available:

```powershell
pwsh ./scripts/run-local-ci.ps1
```

If Docker is not available, note that GitHub Actions CI is the PostgreSQL gate.

### Task E2 - Documentation closeout

Changes:

- Write `docs/dev/milestone-38-underwriting-evidence-documents-learnings.md`.
- Update `docs/project-status.md`.
- Update `CHANGELOG.md`.
- Update `README.md`.
- Update `docs/architecture/overview.md`.
- Update `docs/dev/production-transformation-roadmap.md` if the future milestone sequence needs another
  correction after the implementation.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
```

Commit:

```text
docs: close underwriting evidence documents milestone
```

### Task E3 - PR

Before opening the PR:

```powershell
git log main..HEAD --format=%B
```

Confirm there is no `Co-authored-by`, `Generated with`, robot emoji, or similar AI attribution trailer.

Open a PR into `main` with a plain body:

- what changed
- why it changed
- verification commands/results
- deferred work

Do not merge or squash-merge into `main` from the agent session.
