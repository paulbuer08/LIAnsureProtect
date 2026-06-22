# Milestone 24 - Underwriting Referral Operations Foundation Learnings

This document records the implementation decisions and lessons for `Milestone 24 - Underwriting Referral Operations Foundation`.

## Starting Point

Milestone 24 started from the Milestone 23 closeout commit:

```text
68e094a docs: close underwriting workbench UI milestone
```

Current branch:

```text
codex/milestone-24-underwriting-referral-operations-foundation
```

Milestone 23 added the protected underwriter workbench at `/underwriting/quote-referrals`. That UI could list referred quotes, show risk and expiry triage, request advisory AI review, and submit manual approve, decline, and adjust actions through existing backend endpoints.

## Implemented Scope

Milestone 24 made the referral workflow operationally trackable without changing underwriting authority.

Implemented:

- Backend-owned referral operations records tied one-to-one to referred quotes.
- Default operations creation when a quote becomes `Referred`.
- Risk-based priority and SLA due-date calculation.
- Internal workflow statuses: `New`, `InReview`, `WaitingForInformation`, `Escalated`, `ReadyForDecision`, and `Closed`.
- Self-assignment and assignment release for the current underwriter.
- Triage updates for priority, status, and due date.
- Append-only internal work notes.
- Internal follow-up tasks with open/completed state.
- Lightweight timeline entries for operational evidence.
- Timeline reads that also include final underwriting decisions from `quote_underwriting_reviews`.
- Queue read-model enrichment so the existing workbench can show operations summary fields.
- A minimal React operations panel for assignment, triage, notes, tasks, and timeline.

Still out of scope:

- Document upload and document review.
- Broker/customer messaging.
- Notification inboxes.
- Embeddings, RAG, or production AI credentials.
- Autonomous AI approve/decline/adjust decisions.
- Full analytics dashboards.
- Authority-matrix enforcement.

## Why This Is Realistic Specialty Insurance

Real specialty underwriting work is not only the final decision. A referred account usually moves through an internal operations workflow:

```text
New referral
  -> assigned to an underwriter
  -> triaged by urgency
  -> reviewed
  -> waiting for internal or external information
  -> escalated when needed
  -> ready for a human decision
  -> closed when the final underwriting action is recorded
```

This milestone models that operational layer without pretending to implement every surrounding production system.

The practical value is that an underwriter can now answer:

- Who owns this referral?
- Is it urgent?
- When is it due?
- Is it breaching SLA?
- What internal notes have been recorded?
- What follow-up tasks remain open?
- What changed over time?
- What final decision closed the referral?

## Storage Decision

The milestone uses new operations tables instead of adding many workflow columns directly to `quotes`.

The tables are:

```text
quote_referral_operations
quote_referral_work_notes
quote_referral_follow_up_tasks
quote_referral_timeline_entries
```

Why:

- `quotes` should remain focused on quote terms and underwriting decision state.
- Operations state can grow independently without making the quote row a catch-all record.
- Notes and tasks are naturally child records, not scalar quote fields.
- Timeline entries are append-oriented audit evidence.
- A one-to-one operations record keeps the current milestone simple while allowing richer referral operations later.

Simple analogy:

```text
quotes:
  The quote sheet with terms and decision state.

quote_underwriting_reviews:
  The formal decision log.

quote_referral_operations:
  The internal desk file for managing the referred account.

quote_referral_timeline_entries:
  The running activity log for the desk file.
```

## Default Priority And SLA

When a newly created quote is referred, the system creates an operations record immediately.

Default rules:

```text
High or Severe risk tier
  -> priority High
  -> due in 2 days

Low or Moderate risk tier
  -> priority Normal
  -> due in 5 days

Any quote
  -> due date cannot be later than quote expiry
```

The SLA due date is stored explicitly. That makes later reporting and audit simpler than recalculating historical SLAs from rules that may change over time.

## Notes Are Append-Only

Work notes are append-only in this milestone.

That means:

- Underwriters can add notes.
- Notes cannot be edited.
- Notes cannot be deleted.
- Each note also creates timeline evidence.

Why:

- Underwriting notes are audit-sensitive.
- Editing/deleting notes introduces retention, redaction, permission, and legal-hold questions.
- Append-only notes are easier to reason about and test.

Future milestones can add correction notes or supervisor-only redaction if the product needs it.

## Tasks Are Internal Only

Follow-up tasks are internal operational tasks.

They are not:

- broker requests,
- customer messages,
- document upload requests,
- notification inbox items,
- SLA escalation alerts.

The current fields are intentionally small:

```text
title
due_at_utc
is_completed
created_by_user_id
created_at_utc
completed_by_user_id
completed_at_utc
```

This supports real queue management while avoiding a larger messaging or workflow engine milestone.

## Timeline Shape

The timeline records operational events such as:

- operations created,
- assignment changed,
- priority changed,
- due date changed,
- status changed,
- note added,
- task added,
- task completed,
- decision recorded.

Operational events come from `quote_referral_timeline_entries`.

Final underwriting decision entries are projected from the existing `quote_underwriting_reviews` table. That keeps formal decisions in the existing decision audit table while still showing them in the operations timeline.

## API Shape

The existing referral queue endpoint now includes an operations summary:

```text
GET /api/v1/underwriting/quote-referrals
```

Summary fields:

- assigned underwriter user id,
- priority,
- due date,
- SLA breached flag,
- operations status,
- open follow-up task count,
- latest timeline timestamp.

New operations endpoints:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/operations/assign-to-me
POST /api/v1/underwriting/quote-referrals/{quoteId}/operations/release-assignment
POST /api/v1/underwriting/quote-referrals/{quoteId}/operations/triage
POST /api/v1/underwriting/quote-referrals/{quoteId}/operations/notes
GET  /api/v1/underwriting/quote-referrals/{quoteId}/operations/timeline
POST /api/v1/underwriting/quote-referrals/{quoteId}/operations/tasks
POST /api/v1/underwriting/quote-referrals/{quoteId}/operations/tasks/{taskId}/complete
```

All operations endpoints use the existing `Quotes.Underwrite` policy.

Operations mutations are allowed only while the quote is still `Referred`. After approve, decline, or adjust, the operation is closed and later mutation attempts return conflict behavior.

## Frontend Shape

The React workbench remains a single protected underwriting workflow.

Queue cards now show:

- assignment,
- priority,
- SLA state,
- operations status,
- open task count,
- latest activity.

The selected referral detail now has a separate operations panel for:

- assign to me,
- release assignment,
- triage update,
- add internal note,
- add internal task,
- complete task by id,
- view timeline.

The UI keeps these concepts separate:

```text
Advisory AI review:
  supporting analysis only

Referral operations:
  internal workflow management

Manual decision:
  approve, decline, or adjust quote terms
```

That separation matters because notes and AI output must not look like binding underwriting decisions.

## AI Boundary

Milestone 24 did not expand AI authority.

AI cannot:

- assign a referral,
- change priority,
- change SLA due date,
- add notes,
- create tasks,
- approve a quote,
- decline a quote,
- adjust premium,
- adjust retention,
- accept coverage,
- bind a policy,
- issue a policy.

AI remains advisory context only.

## Testing Lessons

The milestone used test-first coverage for both backend and frontend behavior.

Useful backend test areas:

- default priority and SLA calculation,
- append-only note behavior,
- triage validation,
- task creation and completion,
- operations closure on final underwriting decision,
- underwriter/admin authorization,
- customer/broker denial,
- referral queue operations summary,
- timeline projection,
- conflict behavior after a quote leaves `Referred`,
- migration table and index shape.

Useful frontend test areas:

- queue rendering of operations summary fields,
- operations panel actions,
- timeline rendering,
- separation between operations, advisory AI, and final manual decision controls.

## Verification Commands

Milestone verification uses the standard project path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

During implementation, the frontend dependency directory had to be restored with the repository's `package-lock.json` path because a direct `pnpm exec vitest` attempt left `node_modules` in a partial pnpm-managed state. The safe recovery was to run the package-lock based install for the web project and then run Vitest from `src\LIAnsureProtect.Web` so the web project's jsdom test configuration was used.

## Practical Takeaway

This milestone is more advanced than a basic assignment field, but still bounded.

It adds real underwriting operations concepts that exist in specialty insurance workflows:

- queue ownership,
- urgency,
- SLA tracking,
- internal notes,
- follow-up tasks,
- activity history,
- closure on final decision.

It deliberately avoids the larger systems that should be separate milestones:

- document review,
- broker/customer communication,
- notifications,
- AI document search,
- authority matrices,
- analytics dashboards.
