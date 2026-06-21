# Milestone 21 - Notification And Outbox Publishing Foundation Learnings

This document records the planning notes, implementation decisions, verification path, and closeout notes for `Milestone 21 - Notification And Outbox Publishing Foundation`.

## Starting Point

Milestone 21 starts from the Milestone 20 closeout commit:

```text
4d91665 docs: close quote acceptance and policy binding milestone
```

Milestone 20 added quote acceptance, durable policy binding, simulated binding acknowledgement audit rows, idempotent accept/bind actions, and `PolicyBoundDomainEvent` outbox capture.

That gives Milestone 21 enough real business events to make notification publishing useful.

## Goal

Put the existing transactional outbox to practical use for quote and policy workflow notifications.

In simple English:

- business actions still write domain events into the local database first;
- the Worker reads those durable outbox rows;
- a notification publishing boundary converts selected events into provider-shaped notification messages;
- local test publishing proves the workflow without needing AWS credentials or real email/SMS delivery yet.

## Proposed Scope

Milestone 21 should stay focused on a realistic publishing foundation:

- Add an Application-owned notification publisher boundary.
- Add an Infrastructure local provider-shaped notification publisher.
- Update Worker dispatch behavior so outbox rows are published through the boundary before being marked processed.
- Record enough publish attempt metadata to debug success, retry, and failure behavior.
- Add notification message contracts for important quote and policy workflow events.
- Keep publishing retry-safe so one business action does not create duplicate downstream notifications.
- Add focused backend tests and a migration guard if new persistence is introduced.

Candidate event coverage:

- `QuoteGeneratedDomainEvent`
- `QuoteUnderwritingDecisionRecordedDomainEvent`
- `PolicyBoundDomainEvent`

## Important Boundaries

Notifications are downstream communication side effects. They must not approve, decline, price, accept, bind, cancel, renew, or otherwise change insurance coverage.

The local application database and domain model remain authoritative. External notification systems should be treated as delivery mechanisms, not systems of record.

## Recommended Out Of Scope

- Production SNS/SQS publishing.
- Real email or SMS delivery.
- Notification inboxes.
- Read/unread notification state.
- User notification preferences.
- Complex notification templates.
- Webhooks to broker/customer systems.
- Advisory AI underwriting assistance.

These can become later milestones after the publishing boundary, retry behavior, and operational audit model are stable.

## Likely File Areas

Application:

```text
src/LIAnsureProtect.Application/Notifications/*
src/LIAnsureProtect.Application/Outbox/*
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Notifications/*
src/LIAnsureProtect.Infrastructure/Outbox/*
src/LIAnsureProtect.Infrastructure/Persistence/*
```

Worker:

```text
src/LIAnsureProtect.Worker/*
```

Tests:

```text
tests/LIAnsureProtect.UnitTests/*
tests/LIAnsureProtect.IntegrationTests/*
```

Docs:

```text
README.md
CHANGELOG.md
docs/project-status.md
docs/architecture/overview.md
docs/dev/pattern-roadmap-after-milestone-11.md
docs/dev/milestone-21-notification-and-outbox-publishing-foundation-learnings.md
```

## Starting Verification Path

Use this path before closeout:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

## Closeout

Not started yet.
