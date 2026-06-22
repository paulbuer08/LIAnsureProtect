# Milestone 26 - Evidence Request Notification and Follow-up Foundation Learnings

This document starts the planning and learning notes for `Milestone 26 - Evidence Request Notification and Follow-up Foundation`.

## Starting Point

Branch:

```text
codex/milestone-26-evidence-request-notification-follow-up-foundation
```

Starting commits:

```text
77cad37 feat: add underwriting evidence request foundation
de42ded docs: close underwriting evidence request milestone
```

Milestone 25 added PostgreSQL-backed evidence requests for referred quotes. Underwriters can create evidence requests by cyber-control category, customer/broker owners can respond with text plus safe attachment metadata, underwriters can accept or cancel requests, and referral operations timelines and queue summaries now reflect evidence activity.

## Recommended Scope

Make evidence requests operationally realistic without jumping into production email, inboxes, or full document management.

Good candidates for this milestone:

- Publish local notification messages when an evidence request is created, responded to, accepted, or cancelled.
- Reuse the existing Application notification publishing boundary and outbox processing model from Milestone 21.
- Capture enough recipient/audience metadata to distinguish owner customer/broker notifications from underwriter/requester notifications.
- Add a narrow overdue/follow-up marker or query for open evidence requests whose due date has passed.
- Surface notification/follow-up state in the underwriting workbench where it helps underwriters triage waiting-for-information referrals.
- Keep owner-facing UI changes small, such as a visible due/overdue status on the evidence request page.

## Why This Is Realistic Specialty Insurance

Evidence requests are not useful if nobody knows they exist or if overdue items disappear into the queue.

In real underwriting operations:

- customers and brokers are prompted when an underwriter needs additional evidence,
- underwriters need to know when a response arrives,
- overdue evidence needs follow-up before a referral can progress,
- notification attempts and follow-up activity need audit evidence even before a production email or inbox provider exists.

This milestone should use local provider-shaped notification behavior as the proof point, not production delivery.

## Out Of Scope

Milestone 26 should not become:

- production email/SMS delivery,
- notification inboxes,
- notification preference centers,
- full broker/customer messaging threads,
- real file upload/download or S3 document storage,
- virus scanning,
- OCR/document extraction,
- embeddings or RAG,
- autonomous AI evidence review,
- automated approve/decline/adjust decisions.

AI remains advisory-only. Evidence notification and follow-up should help humans move the workflow, not decide coverage.

## Design Questions For The Milestone 26 Session

- Which evidence request lifecycle events should publish notifications in the first slice?
- Should notification audience targeting use owner user id, requested-by user id, assignment user id, or a small explicit recipient model?
- Should overdue follow-up be computed at read time, stored on the evidence request, or represented as a separate follow-up attempt/audit record?
- Should reminders be manual underwriter actions first, Worker-scheduled local actions first, or only a read-model indicator in this milestone?
- Which existing local notification publisher tests should be extended versus adding evidence-specific tests?
- How much UI should change: simple badges only, or a small follow-up action in the workbench?

## Likely File Areas

Backend:

- `src/LIAnsureProtect.Application/Notifications`
- `src/LIAnsureProtect.Application/Quotes`
- `src/LIAnsureProtect.Domain/Quotes`
- `src/LIAnsureProtect.Infrastructure/Notifications`
- `src/LIAnsureProtect.Infrastructure/Persistence`
- `src/LIAnsureProtect.Worker`
- `tests/LIAnsureProtect.UnitTests`
- `tests/LIAnsureProtect.IntegrationTests`

Frontend:

- `src/LIAnsureProtect.Web/src/features/underwriting`
- `src/LIAnsureProtect.Web/src/features/evidence`

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
