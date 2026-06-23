# Milestone 31 - Notification Inbox Read Model Foundation (Handoff & Planning)

This document is the self-contained handoff for Milestone 31. A fresh session should be able to
continue from this file plus the project docs, without replaying the previous conversation.

> Status: **planning only**. Do not implement until the user approves the Milestone 31 scope.

## Session Handoff Prompt

```text
Workspace path:
  C:\Users\Poy\Documents\LIAnsureProtect

Current branch:
  codex/milestone-31-notification-inbox-read-model-foundation

Created from:
  Milestone 30 closeout commit 1e10753 (docs: close evidence review outcome notification milestone)

Milestone 30 commits (for reference):
  5783085 fix: make referral operations task test time-independent
  12994e4 feat: add evidence review outcome notification foundation
  1e10753 docs: close evidence review outcome notification milestone

Required files to read first:
  docs/project-status.md
  docs/architecture/overview.md  (notification event coverage + Worker dispatcher flow)
  docs/dev/pattern-roadmap-after-milestone-11.md  (Milestone 30 closeout + M31 recommendation)
  docs/dev/milestone-21-notification-and-outbox-publishing-foundation-learnings.md
  docs/dev/milestone-26-evidence-request-notification-follow-up-foundation-learnings.md
  src/LIAnsureProtect.Application/Notifications/  (INotificationPublisher, NotificationMessage, types, audiences)
  src/LIAnsureProtect.Infrastructure/Persistence/Outbox/  (OutboxDispatcher, OutboxNotificationMapper)

Current milestone status:
  Milestone 31 is in planning. Scope below is recommended but not yet approved.

Collaboration rules (from docs/project-status.md):
  - Work milestone by milestone.
  - Do not automatically update code without approval.
  - Before each milestone, explain the design in simple English.
  - Show the intended file/folder changes before implementation.
  - Prefer small, understandable code snippets and explain what each part does.
  - Keep beginner readability and production-style architecture balanced.
  - Update project docs and CHANGELOG.md after meaningful changes.
  - Add or update a milestone learning notes document for the milestone.

Reminder:
  Do NOT implement Milestone 31 until the user approves the scope in simple English first.
```

## Why This Milestone (Simple English)

The last three milestones built the **outgoing mail room**:

```text
Milestone 21: the clerk hands quote/policy envelopes to a local mail provider and records the receipt.
Milestone 26: the clerk also handles evidence-request envelopes.
Milestone 30: the clerk adds "you need to fix something" remediation envelopes.
```

But there is still **no mailbox**. Every notification the system produces is published to a local
sink and then forgotten. A Customer, Broker, or Underwriter cannot open the app and see "here is what
happened on my submissions and quotes."

Milestone 31 builds the **mailbox**: a per-user notification inbox they can read.

```text
Today (sending half only):
  domain event -> outbox_messages -> OutboxDispatcher -> NotificationMessage -> local publisher -> gone

Milestone 31 adds the receiving half (beside the existing publish step):
  NotificationMessage
    -> INotificationInboxRepository
    -> notification_inbox_entries (PostgreSQL)
    -> GET  /api/v1/notifications            (my inbox, newest first)
    -> GET  /api/v1/notifications/unread-count
    -> POST /api/v1/notifications/{id}/read  (mark one read)
    -> React notification bell + list page
```

## Design Principles (consistent with the codebase)

- **PostgreSQL-first**, behind an Application-owned `INotificationInboxRepository`. DynamoDB stays
  deferred. This is the same "simplest durable store first, swap behind the interface later" pattern
  the project already used for `IDocumentStorageService` (local filesystem before S3) and for the
  outbox/idempotency tables. The architecture *plans* DynamoDB for the inbox eventually; M31 keeps the
  interface so that swap stays a later milestone.
- **Fed from the existing dispatcher.** The dispatcher already produces a `NotificationMessage` with an
  audience and a target user id. M31 adds an inbox-writing step **beside** the current publish step; it
  does not change how domain events are produced or mapped.
- **Owner-scoped reads** through `ICurrentUser.UserId`, reusing the Milestone 11 ownership boundary.
- **Idempotent inbox writes** keyed on the source outbox message id, so dispatcher retries do not
  create duplicate inbox entries (reuses the project's idempotency philosophy).
- **Safe payload only.** Persist the same safe attributes the notification already carries (workflow
  ids, category, decision, due date, etc.). No document content, storage keys, or raw file bytes.

## Recommended To-Do Tasks (for the approved implementation)

1. Domain/Application contract
   - `INotificationInboxRepository` in `src/LIAnsureProtect.Application/Notifications/`.
   - Decide the inbox entry shape (DTO/read model). Reuse `NotificationAudiences`/`NotificationMessageTypes`.
2. Persistence
   - New table `notification_inbox_entries` (suggested columns: `id`, `recipient_user_id`, `audience`,
     `type`, `title`/`summary`, `attributes` jsonb, `source_outbox_message_id`, `created_at_utc`,
     `read_at_utc`).
   - EF Core configuration + migration. Index on `(recipient_user_id, read_at_utc)` for unread queries
     and a unique index on `source_outbox_message_id` (+ recipient) for idempotent writes.
3. Dispatcher integration
   - In `OutboxDispatcher`, after a `NotificationMessage` is produced, also write/Upsert an inbox entry
     for the target recipient. Keep it idempotent on the outbox message id.
4. Application reads/commands
   - `ListMyNotificationsQuery` (+ handler), `GetUnreadNotificationCountQuery` (+ handler),
     `MarkNotificationReadCommand` (+ handler). All owner-scoped via `ICurrentUser`.
5. Api
   - `NotificationsController` with a new `Notifications.Read` policy (Application-owned policy name,
     mirroring `Submissions.Read`). Routes: `GET /api/v1/notifications`,
     `GET /api/v1/notifications/unread-count`, `POST /api/v1/notifications/{id}/read`.
6. Web
   - `src/LIAnsureProtect.Web/src/features/notifications` slice: typed API + TanStack Query hooks, a
     notification bell with unread count, and a list page; reuse the Auth0 access-token + guarded-route
     patterns.
7. Tests
   - Unit: inbox entry/read-model behavior. Integration: dispatcher writes an inbox entry; idempotent
     re-dispatch does not duplicate; owner-scoped reads; mark-read flips `read_at_utc`; another user
     cannot read/mark someone else's notification. Frontend: bell unread count + list rendering.
8. Docs
   - Update `README.md`, `CHANGELOG.md`, `docs/project-status.md`, `docs/architecture/overview.md`
     (add the receiving-half flow), and add `docs/dev/milestone-31-...-learnings.md`.

## Out Of Scope (defer to later milestones)

- DynamoDB inbox implementation (keep it a later swap behind the same interface).
- Production email/SMS delivery and a notification preference center.
- Real-time push / SignalR, per-type notification templates, and messaging threads.

## Verification (when implemented)

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1
```
