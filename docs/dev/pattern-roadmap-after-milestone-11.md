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
| Transactional outbox | Implemented in Milestone 13 | `SubmissionSubmittedDomainEvent` is now persisted to PostgreSQL `outbox_messages` in the same save boundary as the submission status change. Dispatch is still deferred. |
| Idempotency | Implemented in Milestone 15 and operationally hardened in Milestone 16 | `POST /api/v1/submissions` and `POST /api/v1/submissions/{submissionId}/submit` now support PostgreSQL-backed `Idempotency-Key` handling. Milestone 16 adds completed-record cleanup so the receipt table does not grow forever. Future important POST endpoints should opt into the same pattern when retries can create duplicate state or side effects. |
| Strategy pattern | Implemented in Milestone 17 | Cyber rating now uses explicit baseline and high-risk strategies behind a selector. |
| Adapter pattern | Implemented in Milestone 19 | Rating provider calls now go through an Application-owned `IRatingProviderClient` and an Infrastructure typed `HttpClient` adapter. |
| Retry and circuit breaker | Implemented in Milestone 19 | `Microsoft.Extensions.Http.Resilience` protects only the outbound rating provider HTTP call. Local EF Core and local rating are not wrapped in this policy. |
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

Status:

```text
Implemented as durable outbox storage. Message dispatch remains planned for Milestone 14.
```

Goal:

```text
Persist domain events to an outbox_messages table in the same database transaction as the submission change.
```

Why this comes after Milestone 12:

- The outbox needs real domain events to store.
- `SubmissionSubmittedDomainEvent` gives the outbox a concrete message shape.

Implemented scope:

- Add `outbox_messages` table through EF Core migration.
- Add an Infrastructure-owned outbox message model.
- Capture domain events during `SaveChangesAsync`.
- Persist serialized outbox messages in the same transaction as the submission update.
- Keep actual message publishing deferred.
- Add tests proving:
  - submitting a submission updates submission status
  - the outbox row is written in the same save boundary
  - committed migrations create the outbox table and pending-message index

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

### Milestone 14 - Outbox Dispatcher Foundation

Status:

```text
Implemented locally as the first Worker-side outbox dispatcher foundation. Notification adapters remain deferred.
```

Goal:

```text
Add the first Worker-side outbox consumer path that reads pending outbox messages and can mark them processed.
```

Why this comes after Milestone 13:

- The outbox table should exist before a dispatcher tries to read from it.
- `processed_at_utc` already exists on `outbox_messages`, so the next smallest useful behavior is to stamp pending rows after local handling.

Implemented scope:

- Added an Infrastructure-owned `IOutboxDispatcher`.
- Added `OutboxDispatcher` that reads pending `outbox_messages` rows ordered by creation time.
- Added `OutboxMessage.MarkProcessed(...)`.
- Updated the Worker host loop to resolve the dispatcher from a scoped dependency-injection scope and run it repeatedly.
- Added tests proving:
  - Infrastructure registration provides the dispatcher
  - the dispatcher marks a pending outbox message processed

Out of scope:

- Notification adapter.
- Real email/SMS provider.
- SNS/SQS.
- Full retry policy.
- Circuit breaker.
- Idempotency keys.
- User notification inbox.
- Quote generation.
- Underwriting queues.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 15 - Idempotent Submission Actions Foundation

Status:

```text
Implemented for the current protected write endpoints.
```

Goal:

```text
Protect important POST actions from duplicate client retries by supporting idempotency keys.
```

Why this comes after submit/outbox:

- Once submit can trigger downstream events/notifications, duplicate requests matter more.
- Idempotency is most useful when a repeated write could cause duplicate side effects.

Implemented scope:

- Added `Idempotency-Key` support for every currently applicable protected POST endpoint:
  - `POST /api/v1/submissions`
  - `POST /api/v1/submissions/{submissionId}/submit`
- Persisted idempotency records in PostgreSQL through `idempotency_records`.
- Stored request key, owner user id, action name, request fingerprint, response status/body/content type/location, and status.
- Returned the same stored response for a repeated matching key.
- Rejected conflicting reuse of the same key for a different body, action, route data, or user.
- Proved safe create retries do not create duplicate draft submissions.
- Proved safe submit retries do not create duplicate outbox messages.
- Added dependency-registration and migration guard coverage for the idempotency service and table.

Out of scope:

- Distributed cache.
- Payment-style idempotency complexity.
- Expiration/cleanup job for old idempotency records.
- Metrics and tracing around replay/conflict/in-progress counts.
- Making `Idempotency-Key` mandatory for all high-risk POST actions.

Future rule:

```text
Every future important protected POST endpoint should be reviewed for idempotency.
If retrying it can create duplicate state or duplicate side effects,
it should use the Milestone 15 idempotency pattern.
```

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 16 - Idempotency Operational Hardening Foundation

Status:

```text
Implemented as the first operational hardening slice for idempotency cleanup/expiry.
```

Goal:

```text
Harden the idempotency foundation so it is safer to operate over time.
```

Why this comes after Milestone 15:

- Milestone 15 added durable idempotency records and safe replay behavior.
- A production-style idempotency system also needs retention, abandoned in-progress handling, observability, and future endpoint conventions.
- Hardening idempotency before adding quote/rating writes keeps later POST actions safer by default.

Implemented scope:

- Add cleanup/expiry behavior for old completed idempotency records.
- Add an Infrastructure-owned cleanup service for `idempotency_records`.
- Add Worker-side cleanup that runs about once per hour.
- Keep completed idempotency records for seven days before deleting them.
- Keep `InProgress` records during cleanup so abandoned-record recovery can be designed explicitly.
- Add a cleanup-query index on `status` and `completed_at_utc`.
- Add focused integration test coverage for cleanup behavior, dependency registration, and migration shape.

Deferred hardening:

- Add explicit recovery behavior for abandoned `InProgress` records.
- Add broader observability for completed, replayed, conflicted, and in-progress idempotency outcomes.
- Decide whether selected high-risk POST endpoints should require `Idempotency-Key`.
- Document the checklist future protected POST endpoints should follow when opting into idempotency.

Out of scope:

- Premium calculation strategy.
- Quote generation.
- SNS/SQS.
- Email.
- Notification inbox/read model.
- Underwriting queues.
- Distributed cache.
- Full payment-provider idempotency semantics.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 17 - Cyber Rating And Quote Foundation

Goal:

```text
Introduce the first realistic local cyber rating and quote slice.
```

Why this comes after submission workflow basics:

- Strategy pattern is useful once rating varies by cyber risk profile.
- Quote creation is a protected POST, so the Milestone 15 idempotency pattern now has another real endpoint to protect.
- Quote generation is the natural bridge between submitted applications, underwriting referral, policy binding, notifications, and advisory AI.

Implemented scope:

- Added a protected endpoint:
  - `POST /api/v1/submissions/{submissionId}/quotes`
- Added local cyber rating inputs:
  - industry class
  - annual revenue band
  - requested limit
  - retention
  - MFA
  - EDR
  - backup maturity
  - incident response plan
  - prior cyber incidents
  - sensitive data exposure
- Added baseline and high-risk cyber rating strategies.
- Added premium factors, risk tiers, subjectivities, referral reasons, and quote status.
- Added PostgreSQL `quotes`.
- Added `QuoteGeneratedDomainEvent`.
- Added idempotency support for quote creation.
- Kept quote creation owner-scoped and limited to submitted submissions.

Out of scope:

- Proprietary insurer rate tables, forms, underwriting manuals, or policy wording.
- Underwriter approval/decline/adjustment workflow.
- External provider calls.
- AI.
- Quote acceptance and policy binding.
- SNS/SQS notification publishing.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 18 - Underwriting Referral Foundation

Status:

```text
Implemented locally as the first underwriter referral workflow around Referred quotes.
```

Goal:

```text
Put realistic underwriter workflow around referred quotes.
```

Why this comes after local rating:

- Milestone 17 can already produce `Referred` quotes and referral reasons.
- A real specialty workflow needs human review before high-risk quotes move forward.
- Underwriter authority should be explicit rather than hidden behind admin or owner bypass behavior.

Implemented scope:

- Add underwriter-only approval, decline, and adjustment actions for referred quotes.
- Add `Quotes.Underwrite` policy for Underwriter and Admin roles.
- Add underwriter-only pending referral queue reads.
- Add audit-friendly reason fields for approval, decline, and adjustment.
- Add current review snapshot fields on quotes.
- Add PostgreSQL `quote_underwriting_reviews` audit history.
- Add `QuoteUnderwritingDecisionRecordedDomainEvent` outbox capture.
- Keep customer/broker ownership separate from underwriter review authority.
- Add tests proving:
  - customers cannot approve their own referred quote
  - underwriters can act only through the underwriter policy
  - adjusted premium/retention/subjectivity changes are persisted with a reason
  - declined quotes cannot be reviewed again

Out of scope:

- External rating provider calls.
- Policy binding.
- AI-generated decisions.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 19 - External Rating Provider Adapter And Resilience Foundation

Status:

```text
Implemented locally as the first external rating provider adapter and resilience foundation.
```

Goal:

```text
Add a provider-shaped external rating call behind an adapter and protect it with retry/circuit-breaker behavior.
```

Why this comes after local rating and referral:

- Adapter is useful when the app has a stable local rating contract to map to a provider-shaped request.
- Retry/circuit breaker belongs around network calls, not local EF Core queries.
- Provider failure should not erase the local quote and referral workflow.

Implemented scope:

- Added `IRatingProviderClient` Application interface.
- Added provider-shaped rating request/result DTOs that carry a market indication instead of a generic ping result.
- Added Infrastructure typed HTTP adapter using `IHttpClientFactory`.
- Added `Microsoft.Extensions.Http.Resilience` standard retry, timeout, and circuit-breaker behavior around the outbound call.
- Added a local simulated provider HTTP handler instead of real insurer credentials.
- Added PostgreSQL `quote_rating_provider_attempts` audit persistence.
- Added safe provider indication fields to the quote creation response.
- Kept local rating, local quote persistence, idempotent quote replay, and underwriting referral behavior available when the provider is unavailable.
- Add tests proving:
  - Application depends on interface, not provider implementation
  - transient provider failure is retried
  - repeated provider failure opens/breaks the circuit
  - provider error maps to safe API/application response
  - provider attempts are persisted
  - idempotent replay does not create duplicate provider attempts

Out of scope:

- Real insurer credentials.
- Production provider onboarding.
- Payment or policy binding.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 20 - Quote Acceptance And Policy Binding Foundation

Status:

```text
Complete locally as the first quote acceptance and policy binding foundation.
```

Goal:

```text
Convert an accepted quote into a bound policy with explicit authority and audit.
```

Why this comes after quotes and underwriting:

- Binding coverage is a high-impact insurance action.
- It needs quote state, authority, idempotency, audit fields, and a separate policy record.
- AI must not bind or issue coverage.

Implemented scope:

- Added `POST /api/v1/quotes/{quoteId}/accept`.
- Added `POST /api/v1/quotes/{quoteId}/bind`.
- Added `Quotes.Accept` and `Policies.Bind` authorization policies.
- Added quote acceptance audit fields.
- Added `Policy` aggregate and PostgreSQL `policies`.
- Added policy number generation, effective date, expiration date, `Bound` status, bound audit fields, and quote term snapshot.
- Added Application-owned `IPolicyBindingProviderClient` and an Infrastructure simulated binding provider.
- Added PostgreSQL `policy_binding_attempts`.
- Added idempotency for both accept and bind actions.
- Added `PolicyBoundDomainEvent` outbox capture.
- Added tests proving eligible acceptance, binding, authorization, idempotent replay, duplicate prevention, persistence, and migration shape.
- Closed with implementation commit `ade6297 feat: add quote acceptance and policy binding foundation` and full local CI artifact `TestResults\local-ci-20260621-210031.zip`.

Out of scope:

- Claims workflow.
- Payment collection.
- AI decision-making.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 21 - Notification And Outbox Publishing Foundation

Status:

```text
Implemented locally as the first notification publishing foundation on top of the transactional outbox.
```

Goal:

```text
Put the existing outbox foundation to real use for quote and policy workflow notifications.
```

Why this comes after quote and policy events:

- The outbox exists and now has more meaningful event types.
- Notification delivery should be built after the business events are real.
- SNS/SQS or a local provider-shaped adapter should be added when there is actual downstream work to publish.

Implemented scope:

- Added an Application-owned `INotificationPublisher` boundary.
- Added provider-shaped `NotificationMessage` contracts and notification type/audience constants.
- Added an Infrastructure local notification publisher for safe tests without AWS credentials or real email/SMS delivery.
- Added `QuoteAcceptedDomainEvent` so quote acceptance becomes a durable outbox event.
- Mapped selected domain events to notification messages:
  - `QuoteGeneratedDomainEvent` with `Quoted` status maps to a customer/broker quote-ready notification.
  - `QuoteGeneratedDomainEvent` with `Referred` status maps to an underwriting-operations review-needed notification.
  - `QuoteUnderwritingDecisionRecordedDomainEvent` maps to a customer/broker decision notification.
  - `QuoteAcceptedDomainEvent` maps to a binding-operations notification.
  - `PolicyBoundDomainEvent` maps to a customer/broker policy-bound notification.
- Updated Worker-side outbox dispatch so selected rows publish before `processed_at_utc` is stamped.
- Added retry/debug metadata on `outbox_messages`: publish attempt count, last publish attempt time, next retry time, provider message id, and poison failure time.
- Added migration/index coverage for the outbox publishing metadata.
- Added tests proving quote acceptance event capture, publish success, transient retry, permanent failure recording, dependency registration, and migration shape.

Out of scope:

- Full user notification inbox.
- Complex email/SMS templates.
- Claims notifications.
- Production SNS/SQS publishing.
- User notification preferences.
- External broker/customer webhooks.

Verification:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 22 - AI Underwriting Assistant Foundation

Goal:

```text
Add advisory-only AI support for underwriting review without allowing AI to make insurance decisions.
```

Why this is later:

- AI needs real submission, quote, referral, and underwriting context to assist with.
- Insurance AI needs governance, audit, explainability, and human oversight.
- AI should support underwriters, not approve, decline, bind, issue, or price coverage by itself.

Planned scope:

Implemented scope:

- Added Application-owned `IAiReviewService`.
- Added provider-shaped request/result DTOs for advisory underwriting review.
- Added an underwriter-only `POST /api/v1/underwriting/quote-referrals/{quoteId}/ai-review` endpoint.
- Added Infrastructure `LocalSimulatedAiReviewService` so the milestone proves the provider shape without real model credentials.
- Added PostgreSQL `ai_underwriting_reviews` for prompt version, output schema version, input snapshot hash, structured advisory output, citations, limitations, status, failure reason, optional feedback, requester, and timestamps.
- Stored structured underwriting packets with executive summary, risk signals, cyber control gaps, suggested underwriting questions, suggested subjectivity candidates, citations, limitations, and advisory disclaimer.
- Added guardrails and tests proving:
  - AI output cannot change quote or policy status directly
  - AI output cannot change premium, retention, subjectivities, underwriting decisions, acceptance, or binding state
  - AI failure still allows manual underwriting
  - stored AI review is visibly advisory

Out of scope:

- Autonomous approve/decline/bind/issue decisions.
- Training custom models.
- Replacing the rating engine with AI.
- RAG over uploaded documents, embeddings, prompt-management UI, and customer-facing AI chat.

Verification:

```text
dotnet test LIAnsureProtect.slnx --no-restore
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

### Milestone 23 - Underwriting Workbench UI Foundation

Status:

```text
Implemented locally as the first protected underwriter workbench on top of the existing referral and advisory AI backend endpoints.
```

Goal:

```text
Add the first protected frontend workbench for underwriters to review referred quotes, request advisory AI review, and record manual underwriting decisions.
```

Why this comes after Milestone 22:

- The backend already has a referral queue, manual approve/decline/adjust endpoints, and advisory AI review.
- The workflow is currently API-only, so the next useful product step is making it usable through the React app.
- Keeping this milestone frontend-focused avoids mixing UI work with new underwriting state-machine rules.

Planned scope:

Implemented scope:

- Added a protected `/underwriting/quote-referrals` route.
- Added a frontend `features/underwriting` slice with typed API functions and TanStack Query hooks.
- Used the existing Auth0 access-token flow and guarded route pattern.
- Added a queue-style workbench that lists referred quotes with risk tier, expiry urgency, premium, limit, retention, referral reasons, and subjectivities.
- Added client-side triage filters for all referrals, high/severe risk referrals, and referrals expiring within seven days.
- Added an advisory AI review panel that shows the executive summary, risk signals, control gaps, suggested underwriting questions, suggested subjectivity candidates, citations, limitations, prompt/schema/hash metadata, and advisory disclaimer.
- Added manual approve, decline, and adjust forms that call the existing backend endpoints and refetch the referral queue after a successful manual action.
- Added focused frontend tests for route registration, list states, triage display, AI review display, advisory wording, mutation payloads, dashboard navigation, and queue refetch behavior.

Out of scope:

- New backend underwriting decision rules.
- AI autonomous decisions.
- Real model credentials.
- RAG, uploaded documents, embeddings, and prompt-management UI.

## Current Recommendation

Continue milestone by milestone. Milestone 24 now gives referred quotes durable operational workflow state around the existing underwriting workbench.

Milestone 25 implements the recommended evidence-request foundation. Milestone 26 implements evidence request notifications and a manual follow-up reminder foundation through the existing local notification/outbox boundary while keeping production email delivery, inboxes, scheduled reminder automation, full document storage, OCR, RAG, and messaging threads out of scope.

The recommended next milestone after document storage was `Milestone 28 - Evidence Document Security Screening Foundation`: add a narrow quarantine-style trust state before evidence documents can be downloaded or accepted for underwriting review.

### Milestone 24 - Underwriting Referral Operations Foundation

Status:

```text
Implemented locally as the first durable underwriting referral operations foundation.
```

Goal:

```text
Make referred quote handling more like a real underwriting operation by adding backend-owned assignment, priority, SLA/due date, work notes, and audit timeline foundations.
```

Why this comes after Milestone 23:

- The workbench now exposes the referral workflow to underwriters.
- Real underwriting teams need operational state around who owns a referral, how urgent it is, what notes were recorded, and what happened over time.
- These concepts need durable backend state and read-model/API changes rather than more frontend-only UI.

Implemented scope:

- Added one-to-one `quote_referral_operations` records for referred quotes.
- Added risk-based default operations creation when quotes become `Referred`.
- Added priority, SLA due date, workflow status, self-assignment, assignment release, and triage update behavior.
- Added append-only internal work notes. Notes are not editable or deletable in this milestone.
- Added internal follow-up tasks with due date, open/completed state, and created/completed audit fields.
- Added `quote_referral_timeline_entries` for operations-created, assignment, priority, due-date, status, note, task, and closure-status evidence.
- Kept final underwriting decisions in `quote_underwriting_reviews`, then merged those decisions into the operations timeline read model.
- Added protected operations endpoints under `/api/v1/underwriting/quote-referrals/{quoteId}/operations`.
- Enriched `GET /api/v1/underwriting/quote-referrals` with assignment, priority, SLA, workflow status, open-task count, and latest timeline fields.
- Updated the React underwriting workbench with a minimal operations panel for assignment, triage, notes, tasks, and timeline.
- Kept advisory AI and final manual decision controls visually separate from operations notes and tasks.

Out of scope:

- Document upload and review.
- Broker/customer messaging.
- Notification inboxes.
- RAG and embeddings.
- Autonomous AI underwriting decisions.
- Production AI credentials.
- Full analytics dashboards.
- Authority-matrix enforcement.

### Milestone 25 - Underwriting Evidence Request Foundation

Status:

```text
Implemented locally as the first customer/broker-facing evidence request workflow for referred quotes.
Implementation commit: 77cad37 feat: add underwriting evidence request foundation
Final local CI artifact: TestResults\local-ci-20260622-225547.zip
```

Goal:

```text
Add the first broker/customer-facing evidence request workflow for referred quotes without turning it into full document management or AI document review.
```

Why this comes after Milestone 24:

- Referral operations now have assignment, status, notes, tasks, SLA, and timeline state.
- Real underwriting teams often need supporting evidence before a final decision.
- `WaitingForInformation` is currently only an internal operations status; Milestone 25 can give that status a concrete, user-facing workflow.

Implemented scope:

- Add underwriting evidence request records tied to referred quotes and referral operations.
- Let underwriters create requests with category, title, description, due date, and status.
- Let the owner customer/broker list requests for their own quote/submission context.
- Let the owner customer/broker submit a text response with respondent audit fields and safe attachment metadata.
- Let underwriters accept or cancel evidence requests.
- Reflect request status back in the underwriting workbench through open/responded counts and latest evidence activity.
- Add timeline entries when evidence requests are created, responded to, accepted, or cancelled.

Out of scope unless explicitly expanded:

- Real file storage in S3.
- Virus scanning.
- OCR and document extraction.
- Embeddings and RAG.
- Autonomous AI document review.
- Notification inboxes.
- Full broker/customer messaging threads.

### Milestone 26 - Evidence Request Notification and Follow-up Foundation

Status:

```text
Implemented locally as the first evidence request notification and manual follow-up foundation.
```

Goal:

```text
Make the evidence request workflow operationally realistic by notifying the right party when evidence is requested or responded to, and by giving underwriters a small follow-up/reminder workflow for overdue evidence.
```

Why this comes after Milestone 25:

- Milestone 25 creates durable evidence request state.
- Real customer/broker evidence workflows need outreach when requests are opened and underwriter visibility when responses arrive.
- Milestone 21 already introduced local notification publishing and outbox retry metadata, so this slice can reuse existing infrastructure without introducing production email or SMS delivery yet.

Recommended first slice:

Implemented scope:

- Published local notification messages when an evidence request is created, responded to, accepted, cancelled, or manually followed up.
- Routed owner notifications to the customer/broker audience and response notifications to underwriting operations through the existing Application notification boundary.
- Added a manual underwriter follow-up action for open evidence requests.
- Recorded follow-up reminder activity in the referral operations timeline.
- Added computed due/overdue fields for owner evidence request reads and underwriting queue summaries.
- Added owner-facing and underwriter-facing UI indicators for overdue evidence and next open evidence due dates.

Out of scope unless explicitly expanded:

- Production email/SMS delivery providers.
- Notification inboxes.
- Full broker/customer messaging threads.
- File upload/download or document storage.
- Virus scanning, OCR, embeddings, RAG, or autonomous document review.
- Automatic approve/decline/adjust decisions.

### Milestone 27 - Evidence Document Storage Foundation

Status:

```text
Implemented locally as the first local evidence document storage foundation.
```

Goal:

```text
Replace evidence attachment metadata placeholders with a narrow local document-storage foundation for uploaded underwriting evidence.
```

Why this comes after Milestone 26:

- Milestone 25 created the evidence request and response workflow.
- Milestone 26 added notifications, follow-up reminders, and due/overdue operational visibility.
- Real cyber underwriting evidence now needs a controlled way to store and retrieve actual supporting files.

Implemented first slice:

- Add an Application-owned document storage boundary.
- Add an Infrastructure local filesystem implementation for development and tests.
- Store safe document metadata in PostgreSQL through `quote_evidence_documents` while keeping file bytes outside the database.
- Allow owner customer/broker multipart upload during evidence response.
- Support up to five files per evidence response, with per-file size, total size, content type, extension, empty-file, and unsafe-name validation.
- Allow authorized owner and underwriter access to retrieve evidence documents through private API download routes.
- Show owner upload controls and underwriter document download links in the React evidence/workbench UI.

Out of scope unless explicitly expanded:

- Production S3 provisioning.
- Public file URLs.
- Virus scanning.
- OCR/document extraction.
- Embeddings, RAG, or autonomous AI document review.
- Legal hold or retention-policy automation.
- Durable document download audit.
- Full document management.

### Milestone 28 - Evidence Document Security Screening Foundation

Status:

```text
Implemented locally as the first evidence document security screening foundation.
```

Goal:

```text
Make uploaded evidence documents more realistic by treating them as quarantined until a provider-shaped scanner marks them clean.
```

Why this comes after Milestone 27:

- Milestone 27 introduced private document storage and API-mediated downloads.
- Real insurance document intake should not trust newly uploaded files immediately.
- A scanner boundary lets the app stay local now while keeping a future S3/GuardDuty or antivirus provider replaceable.

Implemented first slice:

- Added an Application-owned `IEvidenceDocumentScanner` boundary.
- Added an Infrastructure local deterministic scanner for clean, rejected, and failed scan outcomes.
- Persisted current scan metadata on `quote_evidence_documents`: status, provider name, result code, safe result reason, scanned timestamp, and SHA-256 hash.
- Defaulted new and migrated documents to `PendingScan`, then marked newly uploaded files `Clean`, `Rejected`, or `Failed` after local scanning.
- Kept owner and underwriter download routes private, but allowed bytes to stream only for `Clean` documents.
- Blocked underwriter evidence acceptance while any attached document is pending, rejected, or failed.
- Added an owner replacement upload route for responded evidence requests with rejected or failed documents.
- Updated the owner evidence page and underwriting workbench to show scan status and only render clean download links.

Out of scope unless explicitly expanded:

- Production antivirus or SaaS scanner provisioning.
- AWS S3/GuardDuty/EventBridge integration.
- Asynchronous document scanning workers.
- OCR/document extraction.
- Embeddings, RAG, or autonomous AI document review.
- Legal hold or retention-policy automation.
- Manual malware analyst console.
- Full document management.

Verification:

- Focused backend tests passed for scan state transitions, scanner registration, upload persistence, download gating, underwriter acceptance gating, and replacement upload behavior.
- Focused frontend tests passed for owner and underwriter scan labels, disabled download behavior, clean download links, and rejected-document replacement controls.
- `dotnet build LIAnsureProtect.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-restore` passed with UnitTests 52 passed and IntegrationTests 86 passed, 1 skipped PostgreSQL opt-in test.
- EF Core pending model check reported no pending model changes.
- Full local CI passed with artifact `TestResults\local-ci-20260623-160248.zip`.
