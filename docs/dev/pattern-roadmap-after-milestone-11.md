# Pattern Roadmap After Milestone 11

This document audits earlier architecture-pattern recommendations and turns them into a concrete milestone roadmap after `Milestone 11 - Submission Ownership Foundation`.

The goal is to keep useful patterns, but add them only when the product has a feature that genuinely needs them.

Simple rule:

```text
Use the pattern when it solves the current milestone's real problem.
Do not add the pattern just because it is a good interview keyword.
```

## Recommendation Audit

| Recommendation | Current status | Decision |
| --- | --- | --- |
| Vertical Slice for submissions | Implemented | Frontend submission code is under `src/LIAnsureProtect.Web/src/features/submissions`, and backend Application code is organized by submission commands/queries. |
| CQRS read side | Implemented | Milestone 10 added `ListSubmissionsQuery` and `GetSubmissionDetailQuery` with MediatR handlers. |
| REPR-style request/endpoint/response thinking | Implemented as a design lens | Milestone 10 kept controllers, but each read endpoint has a clear route, controller action, MediatR query, and result DTO. This was the intended implementation. |
| Endpoint-per-class REPR framework rewrite | Not implemented | Still not recommended. It would create churn without improving the current learning slice. |
| Minimal API conversion | Not implemented | Still not recommended. The current controller API is clean and already thin. |
| `Submissions.Read` policy | Implemented in Milestone 11 | GET endpoints now use `Submissions.Read` instead of reusing `Submissions.Create`. |
| Submission ownership filtering | Implemented in Milestone 11 | New submissions store `OwnerUserId`; list/detail reads are scoped to `ICurrentUser.UserId`. |
| Domain events | Implemented in Milestone 12 | `Submission.Submit()` now records `SubmissionSubmittedDomainEvent` on the aggregate. Events remain in-memory until the transactional outbox milestone persists them durably. |
| Transactional outbox | Not implemented | Recommended after domain events exist, so there is something real to persist and dispatch reliably. |
| Idempotency | Not implemented | Recommended when create/submit/quote workflows need safe retry behavior. |
| Strategy pattern | Not implemented | Recommended when premium/rating logic exists and variation by product or factor becomes real. |
| Adapter pattern | Not implemented | Recommended when the app calls an external provider or a provider-shaped local fake. |
| Retry and circuit breaker | Not implemented | Recommended only around external network calls, not around local database queries. |
| Cache-aside | Not implemented | Recommended later for expensive dashboard counts or summaries, not for the current basic reads. |
| Saga / process manager | Not implemented | Recommended much later for multi-step underwriting, quote, policy, or claims workflows. |

## Was REPR Implemented In Milestone 10?

Yes, but in the practical way that was recommended.

The recommendation was:

```text
Use vertical-slice organization and REPR-style request/response naming,
but keep controllers for now.
No need to switch to Minimal APIs or endpoint-per-class yet.
```

Current Milestone 10 shape:

```text
GET /api/v1/submissions
  Request: no filter object yet
  Endpoint: SubmissionsController.List
  MediatR: ListSubmissionsQuery
  Response DTO: ListSubmissionsResult
```

```text
GET /api/v1/submissions/{submissionId}
  Request: submissionId route value
  Endpoint: SubmissionsController.GetById
  MediatR: GetSubmissionDetailQuery
  Response DTO: SubmissionDetailResult
```

The only naming difference is that the repo uses `Result` instead of `Response`.
That is acceptable because the Application layer has already established `CreateSubmissionResult`, `ListSubmissionsResult`, and `SubmissionDetailResult`. Renaming them now to `Response` would be churn, not learning value.

## Near-Term Milestone Roadmap

### Milestone 12 - Submission Submit And Domain Events Foundation

Status:

```text
Implemented as the domain-event foundation. Durable outbox dispatch remains planned for Milestone 13.
```

Goal:

```text
Add a real submit action for owned draft submissions and raise the first domain event when a submission is submitted.
```

Why this comes next:

- Domain events should describe important business facts.
- `SubmissionSubmittedDomainEvent` is more meaningful than `SubmissionCreatedDomainEvent` because creating a draft is still private/incomplete.
- The existing `Submission.Submit()` domain method already gives this milestone a natural starting point.

Planned scope:

Implemented scope:

- Added Application command:
  - `SubmitSubmissionCommand`
  - `SubmitSubmissionCommandHandler`
  - `SubmitSubmissionResult`
- Added repository method to load an owned submission for update:
  - `GetOwnedForUpdateAsync(...)`
- Added protected API endpoint:
  - `POST /api/v1/submissions/{submissionId}/submit`
- Required ownership through the same `OwnerUserId` boundary from Milestone 11.
- Added first domain event infrastructure in Domain:
  - `IDomainEvent`
  - aggregate event collection pattern
  - `SubmissionSubmittedDomainEvent`
- Kept event dispatch in-memory/no-op for this milestone.
- Added focused backend tests for:
  - owner can submit own draft
  - other user cannot submit someone else's draft
  - submitted submission raises `SubmissionSubmittedDomainEvent`
  - non-draft submission cannot be submitted again

Out of scope:

- Outbox table.
- Email notification.
- Worker dispatch.
- Quote generation.
- Underwriter queue.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 13 - Transactional Outbox Foundation

Goal:

```text
Persist domain events to an outbox_messages table in the same database transaction as the submission change.
```

Why this comes after Milestone 12:

- The outbox needs real domain events to store.
- `SubmissionSubmittedDomainEvent` gives the outbox a concrete message shape.

Planned scope:

- Add `outbox_messages` table through EF Core migration.
- Add an Application or Infrastructure-owned outbox message model.
- Capture domain events during `SaveChangesAsync`.
- Persist serialized outbox messages in the same transaction as the submission update.
- Keep actual message publishing deferred.
- Add tests proving:
  - submitting a submission updates submission status
  - the outbox row is written in the same save boundary
  - failed validation does not write an outbox message

Out of scope:

- SNS/SQS.
- Background worker publisher.
- Email provider.
- Retry/circuit breaker.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 14 - Outbox Dispatcher And Notification Adapter Foundation

Goal:

```text
Add the first outbox consumer path and a local notification adapter for submission-submitted notifications.
```

Why this comes after Milestone 13:

- The outbox table should exist before a dispatcher tries to read from it.
- A notification adapter gives the Adapter pattern a concrete job.

Planned scope:

- Add an outbox dispatcher service in Worker or Infrastructure.
- Add `INotificationSender` Application interface.
- Add a local fake/logging notification sender in Infrastructure.
- Process `SubmissionSubmittedDomainEvent` outbox messages into notification sends.
- Mark processed messages with processed timestamp or error details.
- Add tests proving:
  - dispatcher reads pending outbox messages
  - known event type triggers the notification adapter
  - processed messages are not sent repeatedly
  - failed send leaves retryable state

Out of scope:

- Real email/SMS provider.
- SNS/SQS.
- Circuit breaker.
- User notification inbox.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 15 - Idempotent Submission Actions Foundation

Goal:

```text
Protect important POST actions from duplicate client retries by supporting idempotency keys.
```

Why this comes after submit/outbox:

- Once submit can trigger downstream events/notifications, duplicate requests matter more.
- Idempotency is most useful when a repeated write could cause duplicate side effects.

Planned scope:

- Add `Idempotency-Key` support for selected POST endpoints.
- Start with `POST /api/v1/submissions/{submissionId}/submit`.
- Persist idempotency records with request key, owner user id, endpoint/action name, response summary, and status.
- Return the same successful response for a repeated matching key.
- Reject conflicting reuse of the same key for a different action or user.
- Add tests for:
  - first request processes normally
  - retry with same key returns same result
  - retry does not add duplicate outbox messages
  - same key from different owner is not accepted

Out of scope:

- Idempotency for every endpoint.
- Distributed cache.
- Payment-style idempotency complexity.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 16 - Premium Calculation Strategy Foundation

Goal:

```text
Introduce a small quote/rating slice where premium calculation varies through explicit strategy classes.
```

Why this comes after submission workflow basics:

- Strategy pattern is useful only when there are real alternative algorithms.
- Premium calculation is the right business area for that lesson.

Planned scope:

- Add first quote request concept for an owned submitted submission.
- Add simple rating inputs that already exist or can be safely derived from current submission data.
- Add `IPremiumCalculationStrategy`.
- Add at least two strategies, for example:
  - simple cyber baseline strategy
  - higher-risk placeholder strategy
- Add Application service to select the strategy.
- Add tests proving:
  - different strategy inputs produce different premium outputs
  - unsupported product/risk shape is rejected clearly
  - quote request remains owner-scoped

Out of scope:

- Real insurer rating logic.
- Binding/issuing policy.
- External provider calls.
- AI.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 17 - External Rating Provider Adapter And Resilience Foundation

Goal:

```text
Add a provider-shaped external rating call behind an adapter and protect it with retry/circuit-breaker behavior.
```

Why this comes after strategy:

- Adapter is useful when the app has something provider-shaped to call.
- Retry/circuit breaker belongs around network calls, not local EF Core queries.

Planned scope:

- Add `IRatingProviderClient` Application interface.
- Add Infrastructure HTTP adapter using `IHttpClientFactory`.
- Use a local fake provider endpoint or test handler first.
- Add retry and circuit-breaker policy around the outbound call.
- Add tests proving:
  - Application depends on interface, not provider implementation
  - transient provider failure is retried
  - repeated provider failure opens/breaks the circuit
  - provider error maps to safe API/application response

Out of scope:

- Real insurer credentials.
- Production provider onboarding.
- Payment or policy binding.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 18 - Dashboard Counts Cache-Aside Foundation

Goal:

```text
Add cache-aside for dashboard summary counts after the app has enough owned data to summarize.
```

Why this waits:

- Current list/detail reads are simple and should stay PostgreSQL-backed.
- Cache-aside is useful when repeated summary reads become expensive or common.

Planned scope:

- Add dashboard summary endpoint for owned submission counts.
- Add cache abstraction such as `ICacheService`.
- Start with in-memory or local Redis depending on setup readiness.
- Cache derived counts only, not sensitive documents or raw claim details.
- Invalidate or refresh counts after relevant submission changes.
- Add tests proving:
  - first read loads from PostgreSQL and stores cache
  - second read can use cache
  - submit/create changes invalidate or refresh the summary

Out of scope:

- Caching private documents.
- Caching raw claim details.
- Distributed invalidation complexity beyond the chosen local pattern.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 19 - Underwriting Workflow Process Manager Foundation

Goal:

```text
Introduce a small multi-step underwriting workflow and coordinate it with a process manager.
```

Why this is later:

- Saga/process manager is only useful when the workflow has multiple steps, state transitions, and follow-up actions.
- The project should first have submission ownership, submit events, outbox, notifications, idempotency, and quote/rating basics.

Planned scope:

- Add an underwriting review workflow state.
- React to submission-submitted or quote-requested events.
- Assign or create an underwriting task.
- Track workflow state separately from the `Submission` aggregate when appropriate.
- Add tests proving:
  - event starts the workflow once
  - repeated event does not duplicate workflow state
  - workflow progresses through expected states
  - failed downstream action can be retried safely

Out of scope:

- Full production underwriting workbench.
- Complex human assignment rules.
- Claims workflow.
- Real insurer integration.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

## Current Recommendation

Do not add any of these larger infrastructure patterns into the current Milestone 11 implementation.

Milestone 11 already added an important security boundary:

```text
Authentication + role policy decides whether the caller can enter.
Ownership filtering decides which submission rows the caller can see.
```

That should be closed, verified, documented, and committed before starting domain events or outbox work.
