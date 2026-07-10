# Chapter 6 — Flow: Submission Intake

**Trigger:** a Customer/Broker fills the intake form (`/submissions/new`) — or later presses
**Submit** on a draft.
**Result:** a `submissions` row owned by the caller; on submit, a status change **plus** an
outbox event, committed together.

> **Analogy:** filling in a paper application (draft), then dropping it into the office mailbox
> (submit). The mailroom clerk (outbox + worker) guarantees the "new application!" memo reaches
> every department — even if the clerk was on a break when it was dropped.

## Part A — Creating a draft submission

```mermaid
sequenceDiagram
    autonumber
    actor User as Maria (Broker)
    participant Form as NewSubmissionPage →<br/>SubmissionIntakeForm.tsx
    participant Hook as useCreateSubmission.ts<br/>(TanStack mutation)
    participant Api as createSubmission.ts
    participant Ctrl as SubmissionsController.Create<br/>POST /api/v1/submissions
    participant Idem as EfCoreIdempotencyService
    participant Val as ValidationBehavior<br/>(CreateSubmissionCommandValidator)
    participant H as CreateSubmissionCommandHandler
    participant Dom as Submission.CreateDraft(...)
    participant Repo as EfCoreSubmissionRepository
    participant UoW as EfCoreUnitOfWork → SubmissionDbContext

    User->>Form: fill applicant/company fields, click Create
    Form->>Form: Zod schema validates client-side (submissionIntakeSchema)
    Form->>Hook: mutate(formData)
    Hook->>Api: POST with bearer token + Idempotency-Key
    Api->>Ctrl: HTTP request
    Note over Ctrl: [Authorize(Policy = Submissions.Create)]
    Ctrl->>Idem: ExecuteAsync(key, fingerprint, operation)
    Idem->>Idem: known key? → replay stored response and stop
    Idem->>Val: sender.Send(CreateSubmissionCommand)
    Val->>Val: FluentValidation — fail → 400 ProblemDetails
    Val->>H: Handle(command)
    H->>Dom: Submission.CreateDraft(name, email, company, ownerUserId)
    H->>Repo: AddAsync(submission)
    H->>UoW: SaveChangesAsync — INSERT + idempotency record commit together
    Ctrl-->>Api: 201 Created + Location /api/v1/submissions/{id}
    Hook->>Hook: invalidate submissions query cache
    Form-->>User: navigate to detail page
```

Key points, mapped to code:

- **Two validation layers on purpose.** Zod (`schemas/submissionIntakeSchema.ts`) gives instant
  feedback; FluentValidation is the *authoritative* gate — the API never trusts the browser.
- **Ownership is stamped at birth.** The handler takes `ICurrentUser.UserId` as `OwnerUserId`;
  every later read is filtered by it (Chapter 5).
- **Idempotency** (`SubmissionsController.Create`, lines with `GetIdempotencyKey()`): with an
  `Idempotency-Key` header, the whole operation runs inside `EfCoreIdempotencyService.ExecuteAsync`
  — a SHA-256 **fingerprint** of method + route + body is stored, the response body/status/location
  are recorded, and an identical retry **replays** the stored response instead of creating a
  duplicate. Same key + *different* payload → `409 Conflict`.

## Part B — Reading (list & detail)

`GET /api/v1/submissions` → `ListSubmissionsQueryHandler`; `GET /api/v1/submissions/{id}` →
`GetSubmissionDetailQueryHandler`. Both are **no-tracking** EF Core projections filtered by
`ICurrentUser.UserId`, rendered by `SubmissionsPage.tsx` / `SubmissionDetailPage.tsx` via TanStack
Query hooks (`useSubmissions`, `useSubmissionDetail`). Detail also returns the latest owned Quote
snapshot when one exists, so customer quote/accept/bind state survives a browser refresh.

A Draft may be edited before submission through owner-scoped
`PUT /api/v1/submissions/{submissionId}`. The aggregate guard rejects edits after Draft, and the
detail UI exposes edit/save/cancel separately from the final Submit command. This preserves the
important product distinction between "save my unfinished application" and "send it for rating".

## Part C — Submitting the draft (where events begin)

`POST /api/v1/submissions/{submissionId}/submit` (`SubmissionsController.Submit`, policy
`Submissions.Submit`, idempotent like create):

```mermaid
sequenceDiagram
    autonumber
    participant H as SubmitSubmissionCommandHandler
    participant Repo as EfCoreSubmissionRepository<br/>(owned, tracked load)
    participant Dom as Submission.Submit()
    participant Ctx as SubmissionDbContext.SaveChangesAsync

    H->>Repo: load submission (owner-scoped, tracked)
    H->>Dom: submission.Submit()
    Note over Dom: guard: only a Draft may submit →<br/>status = Submitted +<br/>records SubmissionSubmittedDomainEvent
    H->>Ctx: SaveChangesAsync(...)
    Note over Ctx: collects domain events from tracked aggregates →<br/>writes outbox_messages row →<br/>UPDATE submissions + INSERT outbox<br/>in ONE transaction
```

This is the **transactional outbox** in action (the pattern every later flow reuses):
`Submission.Submit()` records the event in memory; `SubmissionDbContext.SaveChangesAsync`
serializes it into `outbox_messages` inside the same transaction as the status change. What
happens to that row — the Worker, the dispatcher, notifications — is
[Chapter 10](10-flow-notifications-and-background.md).

After submission, the Submission remains the historical application record. Quote and Policy are
separate aggregates, so later quote acceptance or policy binding does not rewrite Submission status
from `Submitted` to `Bound`. Submission detail now shows three separate sections — Submission,
Latest quote, and Related policy — plus a derived journey-stage label for scanning. The label never
mutates or replaces an aggregate's real status.

## Part D — Deleting drafts, withdrawing applications, and duplicate warnings

Lifecycle cleanup is deliberately asymmetric because an insurance application becomes audit history
once submitted:

- `DELETE /api/v1/submissions/{id}` is owner-scoped and accepts only `Draft`. The UI asks for explicit
  confirmation. A submitted record returns `409` and remains stored.
- `POST /api/v1/submissions/{id}/withdraw` is owner-scoped and idempotent. Only a `Submitted`
  application may transition to `Withdrawn`, and only before an accepted or bound Quote exists.
  `SubmissionWithdrawnDomainEvent` is captured in the transactional outbox in the same commit, so
  the retained row and audit event cannot disagree.
- Creating a draft checks for another open Draft/Submitted record for the same owner and company.
  The response sets `possibleDuplicate=true` and the UI warns the user, but the draft is still
  created. Multiple legitimate submissions are a supported business case.

Quote decline/expiry and Policy cancellation remain separate lifecycle concepts. Withdrawal never
overloads Quote or Policy status, and a bound Policy is never deleted.

**Failure honesty:** if the transaction fails, *both* the status change and the event vanish —
never one without the other. If the process crashes right after commit, the event is safely on
disk and dispatch happens after restart. Delayed, never lost.

## What can go wrong (and what the user sees)

| Situation | Where it's caught | Response |
|---|---|---|
| Invalid form data | Zod (browser), then `ValidationBehavior` | inline errors / `400` with field errors |
| Not logged in / wrong role | JWT middleware / policy | `401` / `403` |
| Submitting someone else's draft | owner-scoped load returns null | `404` (existence is not leaked) |
| Submitting a non-draft | `Submission.Submit()` guard throws `InvalidOperationException` | `409` ProblemDetails |
| Deleting submitted history | delete handler state guard | `409`; row retained |
| Withdrawing after quote acceptance/bind | repository eligibility check | `409`; Submission and contract history unchanged |
| Network retry / double click | idempotency replay | the original response, again |
