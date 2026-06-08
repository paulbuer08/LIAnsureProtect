# Milestone 4 - Application Use Case Foundation Learnings

This document records the decisions and tradeoffs from Milestone 4 - Application Use Case Foundation.

## Why This Milestone Exists

Milestone 3 created the setup doors:

```text
AddApplication()
AddInfrastructure()
```

Milestone 4 starts putting real business workflow code behind those doors.

The first business slice is submission intake. A submission is the front door of the insurance workflow: a customer or broker starts an application before underwriting, quotes, policies, claims, documents, or AI review exist.

## Practical CQRS

The project uses practical CQRS in the Application layer.

Simple meaning:

- Commands ask the system to change something.
- Queries ask the system to read something.

Milestone 4 starts with a command:

```text
CreateSubmissionCommand
```

This does not mean separate read and write databases. PostgreSQL remains the planned single system of record.

## MediatR

MediatR is the in-process dispatcher.

Simple analogy:

```text
Controller or Worker
  -> receptionist
  -> correct use-case handler
```

The API or Worker does not need to know the handler class directly. It sends a command or query, and MediatR routes it to the right handler.

Current flow:

```text
POST /api/v1/submissions
  -> SubmissionsController
  -> CreateSubmissionCommand
  -> ValidationBehavior
  -> CreateSubmissionCommandHandler
  -> Submission.CreateDraft(...)
  -> ISubmissionRepository.AddAsync(...)
```

## FluentValidation And The Validation Pipeline

FluentValidation checks command/query request models before handlers run.

The validation pipeline behavior is like a form checker at the front desk:

```text
Request arrives
  -> check required fields and input shape
  -> if invalid, stop before the handler
  -> if valid, continue to the handler
```

This keeps handlers focused on business workflow instead of repeating input checks.

Domain objects still protect business rules. Validation is not a replacement for domain invariants.

## API Endpoint

Milestone 4 exposes the first business endpoint:

```text
POST /api/v1/submissions
```

The controller is thin.

Simple analogy:

```text
Controller:
  "I receive the form at the window."

MediatR/Application:
  "I decide which business desk handles it."

Domain:
  "I protect the business rules."
```

The controller translates JSON into `CreateSubmissionCommand`. If the validation pipeline rejects the command, the controller returns `400 Bad Request` with validation problem details.

## Submission Domain Model

`Submission` uses a private constructor and a public factory method:

```text
Submission.CreateDraft(...)
```

Simple analogy:

- The private constructor is the staff-only entrance.
- `CreateDraft`, `Submit`, and `Withdraw` are the public doors with rules.

`Status` has a private setter so other code can read the status but cannot directly change it.

Valid first statuses:

- `Draft`: the submission exists but is not formally submitted.
- `Submitted`: the submission has been sent for the next workflow step.
- `Withdrawn`: the applicant or broker stopped the application before it became a policy.

The word `Withdrawn` is preferred over `Cancelled` for applications because cancellation often means ending an active policy.

## Repository Interface

`ISubmissionRepository` lives in Application.

This is a promise the Application layer needs:

```text
"I need something that can save submissions."
```

The Application layer should not know whether that storage is PostgreSQL, local memory, or a test double.

## Temporary In-Memory Repository

Milestone 4 adds a temporary `InMemorySubmissionRepository` in Infrastructure.

Reason:

- MediatR registers the create-submission handler.
- The handler depends on `ISubmissionRepository`.
- ASP.NET Core validates services when the app starts in development and tests.
- Without an implementation, even health endpoint integration tests cannot start the API host.

Simple analogy:

```text
ISubmissionRepository:
  "I need a filing tray."

InMemorySubmissionRepository:
  "Use this temporary desk tray until the real filing cabinet arrives."
```

This is not the future production persistence design. PostgreSQL remains the system of record. The in-memory repository should be replaced when the persistence milestone adds EF Core and PostgreSQL.

## Moq

Moq is used in Milestone 4 because the handler depends on `ISubmissionRepository`.

The handler test uses Moq to verify:

- the handler creates a draft submission
- the handler passes that submission to `ISubmissionRepository.AddAsync(...)`
- the repository is called exactly once

Use Moq when it helps replace an interface dependency in a focused unit test. Do not use Moq when a simple real object or direct validator test is clearer.

## Unit Of Work

Unit of Work is intentionally not added in Milestone 4.

Reason:

- Unit of Work coordinates persistence changes.
- The project does not have EF Core, PostgreSQL, a `DbContext`, or transactions yet.
- Adding it now would create a placeholder with no real work to coordinate.

Recommended future placement:

```text
Later persistence milestone
  -> EF Core DbContext
  -> PostgreSQL mapping
  -> SubmissionRepository
  -> IUnitOfWork
  -> EF Core UnitOfWork
```

Likely future handler shape:

```text
submissionRepository.Add(submission)
unitOfWork.SaveChangesAsync(...)
```

That design will make more sense once the database exists.

## What Was Intentionally Not Added

Milestone 4 does not add:

- authentication
- authorization policies
- database schema
- EF Core `DbContext`
- PostgreSQL migrations
- React frontend
- cloud infrastructure
- domain events
- transactional outbox
- event sourcing

Those belong in later approved milestones.

## What To Remember

Use MediatR for Application commands and queries.

Use FluentValidation for command/query input validation.

Use the validation pipeline so handlers receive already-validated requests.

Keep repository interfaces in Application when handlers need persistence promises.

Keep Infrastructure implementations outside Application.

Use the temporary in-memory repository only until real persistence exists.

Add Unit of Work when EF Core/PostgreSQL persistence is introduced.
