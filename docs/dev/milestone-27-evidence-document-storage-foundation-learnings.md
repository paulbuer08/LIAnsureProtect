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

The recommended Milestone 27 target is the first real evidence document storage slice.

Good candidates for this milestone:

- Add an Application-owned document storage boundary for evidence attachments.
- Add a local filesystem storage implementation for development and tests before production S3.
- Replace evidence response attachment metadata placeholders with a narrow upload path for one evidence file per response or a small bounded attachment set.
- Store safe file metadata in PostgreSQL beside the evidence request or in a small evidence document table.
- Add private owner and underwriter download/read access behind existing authorization rules.
- Record audit-friendly storage metadata such as file name, content type, size, storage key, uploaded by user id, and uploaded timestamp.
- Keep the file bytes out of PostgreSQL.
- Keep the first storage path local and provider-shaped so S3 can replace it later without rewriting Application use cases.

## Why This Is Realistic Specialty Insurance

Cyber underwriters usually need real supporting documents, not only text responses. Examples include MFA screenshots, EDR deployment reports, backup test evidence, incident response plans, prior loss details, and questionnaire clarifications.

Milestone 25 created the evidence request workflow. Milestone 26 made the workflow operational with notifications and follow-ups. Milestone 27 can now make evidence responses more realistic by giving the app a controlled way to store and retrieve uploaded evidence documents.

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

## Design Questions For The Milestone 27 Session

- Should the first slice support one uploaded evidence file per response, or a small bounded list of attachments?
- Should document metadata live directly on `quote_evidence_requests` at first, or in a separate `quote_evidence_documents` table?
- What should the local storage root be for development and tests?
- Should downloads be streamed through the API only, or should the first slice return a short-lived local reference shape for later S3 presigned URLs?
- How should tests prove owner scoping and underwriter access without exposing another owner's documents?
- Which file size and content type checks should be included in the first slice?

## Likely File Areas

Backend:

- `src/LIAnsureProtect.Application/Documents` or `src/LIAnsureProtect.Application/Evidence`
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
