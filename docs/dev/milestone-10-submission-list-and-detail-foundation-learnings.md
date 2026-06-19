# Milestone 10 - Submission List And Detail Foundation Learnings

This document records the planning decisions, design-pattern tradeoffs, implementation notes, verification results, and lessons for `Milestone 10 - Submission List And Detail Foundation`.

At the start of the milestone, Milestone 9 already lets a signed-in user create a draft submission from `/submissions/new`. The next useful product step is not quote generation yet. It is letting the user find and inspect the draft submissions that already exist.

## Starting Point

Milestone 10 starts from:

```text
689df5b feat: add submission intake UI foundation
```

Branch:

```text
codex/milestone-10-submission-list-and-detail-foundation
```

Current create flow:

```text
Signed-in browser user
  -> /submissions/new
  -> Auth0 access token
  -> POST /api/v1/submissions
  -> SubmissionsController
  -> CreateSubmissionCommand
  -> ValidationBehavior
  -> CreateSubmissionCommandHandler
  -> Submission.CreateDraft(...)
  -> ISubmissionRepository.AddAsync(...)
  -> IUnitOfWork.SaveChangesAsync(...)
  -> PostgreSQL
```

Milestone 10 adds the read side:

```text
Signed-in browser user
  -> /submissions
  -> Auth0 access token
  -> GET /api/v1/submissions
  -> ListSubmissionsQuery
  -> EF Core no-tracking read
  -> PostgreSQL
```

```text
Signed-in browser user
  -> /submissions/{submissionId}
  -> Auth0 access token
  -> GET /api/v1/submissions/{submissionId}
  -> GetSubmissionDetailQuery
  -> EF Core no-tracking read
  -> PostgreSQL
```

## Why This Milestone Exists

Creating data without a way to read it back is only half a workflow.

Milestone 9 proved:

- the frontend can collect submission data
- Auth0 can provide an API access token
- the API can authorize the request
- the Application layer can create a draft submission
- PostgreSQL can persist the submission

Milestone 10 should prove:

- the frontend can load protected server data
- the API can expose protected read endpoints
- the Application layer can model read use cases as queries
- Infrastructure can read from PostgreSQL without tracking changes
- the user can navigate from dashboard to list to detail

## Pattern Decision: REPR / Vertical Slice

REPR means:

```text
Request -> Endpoint -> Response
```

It is applicable to this project, but Milestone 10 should use it as a design lens rather than a framework rewrite.

Current API style:

```text
Controller action
  -> MediatR request
  -> Application handler
  -> response DTO
```

That is already close to REPR because each endpoint can still have a clear request and response shape.

Milestone 10 should not rewrite `SubmissionsController` into Minimal APIs or endpoint-per-class handlers. That would create churn without improving the learning goal. The useful lesson is to keep each endpoint easy to reason about:

```text
GET /api/v1/submissions
  Request: no filters yet
  Endpoint: SubmissionsController.List
  Query: ListSubmissionsQuery
  Response: ListSubmissionsResult
```

```text
GET /api/v1/submissions/{submissionId}
  Request: route id
  Endpoint: SubmissionsController.GetById
  Query: GetSubmissionDetailQuery
  Response: SubmissionDetailResult or 404
```

## Pattern Decision: CQRS + MediatR

CQRS is already part of the project.

The create path uses a command because it changes state:

```text
CreateSubmissionCommand
```

Milestone 10 should use queries because list/detail reads do not change state:

```text
ListSubmissionsQuery
GetSubmissionDetailQuery
```

This keeps a useful mental model:

```text
Commands answer: "Please do this."
Queries answer: "Please show me this."
```

## Pattern Decision: EF Core No-Tracking Reads

EF Core normally tracks entities it loads so it can detect changes before `SaveChangesAsync`.

For list and detail screens, the app only needs to display data. It does not need EF Core to watch those objects for updates.

So Milestone 10 should use:

```csharp
AsNoTracking()
```

Simple rule:

```text
Changing data? Tracking can be useful.
Showing data only? No tracking is usually better.
```

## Pattern Decision: What To Defer

The following patterns are useful, but they should not be added in Milestone 10.

| Pattern | Decision | Reason |
|---|---|---|
| Domain Events | Defer | Useful when a business action should announce something happened, such as submission submitted. List/detail reads do not need that. |
| Outbox | Defer | Useful when domain events must be published reliably after a database transaction. No async side effect is being created yet. |
| Inbox | Defer | Useful when consuming external messages. The app is not consuming messages yet. |
| Idempotency | Defer | Useful when retries could duplicate an important write. Milestone 10 mostly adds reads. |
| Saga / Process Manager | Defer | Useful for multi-step workflows such as underwriting or quote orchestration. List/detail is not a long-running process. |
| Cache-Aside | Defer | Useful for expensive or high-volume reads. Basic submission reads should use PostgreSQL first. |
| Strategy Pattern | Defer | Best for premium calculation when rating rules vary by product or factor. |
| Adapter Pattern | Defer | Best when calling external providers such as insurers, rating APIs, storage, email, or payments. |
| API Gateway | Defer | Mostly a deployment and platform boundary concern. The local monolith does not need it yet. |
| BFF | Defer | Useful when frontend needs server-side composition or cookie/session mediation. The current SPA-to-API Auth0 flow is still simple. |
| Retry + Circuit Breaker | Defer | Useful around external network calls. Milestone 10 does not call external providers. |

Important lesson:

```text
A pattern is valuable when it removes a real problem.
A pattern is noise when it is added before the problem exists.
```

## Planned Milestone 10 Tasks

The detailed task-by-task implementation plan is here:

```text
docs/superpowers/plans/2026-06-19-milestone-10-submission-list-and-detail-foundation.md
```

High-level order:

1. Document the Milestone 10 boundary.
2. Add Application query contracts.
3. Add EF Core read repository methods.
4. Add protected API read endpoints.
5. Add frontend read API functions and hooks.
6. Add submission list page.
7. Add submission detail page.
8. Wire routes and dashboard navigation.
9. Run full verification.
10. Close docs and changelog.

## What Was Implemented

Backend:

- Added `ListSubmissionsQuery`.
- Added `ListSubmissionsQueryHandler`.
- Added `ListSubmissionsResult` and `SubmissionListItemResult`.
- Added `GetSubmissionDetailQuery`.
- Added `GetSubmissionDetailQueryHandler`.
- Added `SubmissionDetailResult`.
- Extended `ISubmissionRepository` with:

```csharp
Task<IReadOnlyList<SubmissionListItemResult>> ListAsync(CancellationToken cancellationToken);

Task<SubmissionDetailResult?> GetDetailAsync(
    Guid submissionId,
    CancellationToken cancellationToken);
```

- Implemented EF Core read methods in `EfCoreSubmissionRepository`.
- Added `GET /api/v1/submissions`.
- Added `GET /api/v1/submissions/{submissionId}`.
- Added `404 Not Found` behavior when a submission detail is missing.
- Added backend unit tests for the query handlers.
- Added backend integration tests for protected list/detail endpoint behavior.

Frontend:

- Added `listSubmissions(...)`.
- Added `getSubmissionDetail(...)`.
- Added `useSubmissions()`.
- Added `useSubmissionDetail(...)`.
- Added protected `/submissions` route.
- Added protected `/submissions/:submissionId` route.
- Added `SubmissionsPage`.
- Added `SubmissionDetailPage`.
- Updated dashboard navigation with both "View submissions" and "Create submission".
- Added frontend tests for list loading, empty, error, success, detail loading, not-found, error, success, and dashboard navigation.

## LINQ And EF Core Notes

Milestone 10 is a good first hands-on LINQ read milestone.

The EF Core repository uses:

```csharp
AsNoTracking()
```

Why:

- The list and detail pages only display data.
- The code does not update the loaded `Submission` objects.
- EF Core does not need to track changes for these objects.
- This reduces unnecessary change-tracker work.

The list query uses:

```csharp
OrderByDescending(submission => submission.CreatedAtUtc)
```

Why:

- Users usually expect the newest submissions first.
- Sorting happens in the database instead of after loading everything into memory.

The detail query uses:

```csharp
Where(submission => submission.Id == submissionId)
```

Why:

- The database should filter to the requested row.
- The app should not load every submission and then find one in memory.

Both read methods use:

```csharp
Select(...)
```

Why:

- The API response does not need every possible domain object behavior.
- Projection keeps the read model focused on the fields the UI needs.
- This is a practical first example of shaping data for a screen.

The repository intentionally does not use:

```csharp
Include(...)
AsSplitQuery()
```

Why:

- `Submission` currently has no navigation properties or related collections.
- There is no related object graph to eager-load.
- `AsSplitQuery()` helps when eager-loading multiple related collections could create a large joined result. That problem does not exist yet.

The repository also intentionally does not use:

```csharp
HasQueryFilter(...)
```

Why:

- Ownership filtering is important later.
- The project does not yet have internal user/profile ownership data tied to submissions.
- Adding a global query filter before the ownership model exists would teach the wrong lesson.

Simple rule:

```text
Use the LINQ/EF Core feature that solves the current data-access problem.
Do not add advanced query features just to show them.
```

## Authorization Note

Milestone 10 reuses the existing `Submissions.Create` policy for read endpoints.

This is not the final security model. It is a deliberate simplification because Milestone 10 is focused on the first read workflow.

Future direction:

```text
Submissions.Create -> can create submissions
Submissions.Read   -> can read submissions
```

Later, ownership rules should decide which submissions a `Customer` or `Broker` can read. That likely needs internal user/profile data first.

## Verification Notes

Commands run during implementation:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
```

Results:

- Backend build passed with 0 warnings and 0 errors.
- Backend UnitTests passed: 17 passed.
- Backend IntegrationTests passed: 17 passed, 1 skipped PostgreSQL opt-in test.

From `src/LIAnsureProtect.Web`:

```powershell
tsc -b
eslint .
vitest run
vite build
```

Results:

- TypeScript compile passed.
- ESLint passed.
- Vitest passed: 5 test files, 16 tests.
- Vite production build passed.
- Vite emitted separate route chunks for `SubmissionsPage` and `SubmissionDetailPage`, preserving the route-level code-splitting direction from Milestone 9.

Final local CI command:

```powershell
.\scripts\run-local-ci.ps1
```

Codex local CI command actually used:

```powershell
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

Why `-RunFrontendInstall:$false` was used:

- This Codex shell does not expose a normal `npm` executable on PATH.
- The project already had `node_modules` installed from the lockfile.
- A temporary PowerShell `npm` function delegated `npm run build`, `npm run lint`, and `npm run test` through the bundled package runner.
- `npm ci` was separately run through the bundled package runner to repair `node_modules` after an accidental `pnpm run test` attempt moved packages into `node_modules/.ignored`.

Local CI result:

- Passed.
- Artifact zip: `TestResults\local-ci-20260619-172803.zip`.
- The script started PostgreSQL/pgvector, applied the committed migration, ran backend tests, validated Docker Compose config, ran frontend build/lint/tests, produced the artifact zip, and cleaned up the PostgreSQL container, volume, and network.

Important package-manager lesson:

```text
Do not run pnpm directly inside this npm/package-lock project.
```

The frontend project is currently npm-lockfile based. Running `pnpm run ...` can try to take ownership of `node_modules` and move npm-installed packages into `node_modules/.ignored`. If the Codex environment lacks `npm`, prefer invoking npm itself through the bundled package runner:

```powershell
C:\Users\Poy\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd dlx npm@11.7.0 run build
```

Manual browser smoke still should prove before final user closeout:

1. A signed-in user can create a draft submission.
2. The new draft appears in `/submissions`.
3. The user can open `/submissions/{submissionId}`.
4. The detail page shows the expected applicant, email, company, status, id, and timestamp.
