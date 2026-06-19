# Milestone 10 - Submission List And Detail Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first protected read workflow for submissions so a signed-in user can view created draft submissions in a list and open a detail page.

**Architecture:** Keep the existing controller-based ASP.NET Core API and continue the practical CQRS + MediatR pattern already used by create submission. Apply REPR-style request/response thinking and vertical-slice organization without rewriting the API to Minimal APIs or endpoint-per-class handlers.

**Tech Stack:** ASP.NET Core Web API, .NET 10, MediatR, FluentValidation, EF Core/PostgreSQL, React 19, TypeScript, Vite, React Router, Auth0 React SDK, TanStack Query, Vitest, React Testing Library, xUnit.

---

## Scope Summary

Milestone 9 lets a signed-in user create a draft submission at `/submissions/new`.

Milestone 10 should answer the next natural user question:

```text
"I created a submission. Where can I see it again?"
```

This milestone adds:

- `GET /api/v1/submissions` for a protected submission list.
- `GET /api/v1/submissions/{submissionId}` for a protected submission detail view.
- Application query handlers for list and detail reads.
- EF Core read methods on the existing submission repository.
- Protected frontend routes `/submissions` and `/submissions/:submissionId`.
- TanStack Query read hooks for loading, empty, error, success, and not-found states.
- Dashboard navigation to both "View submissions" and "Create submission".
- Focused backend, frontend, and docs updates.

This milestone intentionally does not add:

- Ownership filtering by current user.
- Fine-grained permission strings.
- Editing submissions.
- Submit-for-underwriting workflow.
- Multi-step questionnaire.
- File upload/document storage.
- Quote generation.
- Domain events.
- Outbox/inbox.
- Saga/process manager.
- Cache-aside.
- External provider adapters.
- Retry/circuit breaker.
- API Gateway or BFF.

Those patterns are useful later, but adding them here would hide the main lesson: building a clean read side after the create side already exists.

## Design Pattern Notes

### REPR / Vertical Slice

Use REPR thinking for each API action:

```text
Request -> Endpoint -> Response
```

For this project, that means each endpoint should have a clear request shape, a thin controller method, a MediatR query, and a response DTO. It does not mean replacing the existing controller style yet.

Milestone 10 examples:

```text
GET /api/v1/submissions
  Request: no filters yet
  Endpoint: SubmissionsController.List
  Query: ListSubmissionsQuery
  Response: ListSubmissionsResponse
```

```text
GET /api/v1/submissions/{submissionId}
  Request: submissionId route value
  Endpoint: SubmissionsController.GetById
  Query: GetSubmissionDetailQuery
  Response: SubmissionDetailResponse or 404
```

### CQRS + MediatR

Continue the current pattern:

- Commands change state.
- Queries read state.
- Handlers live in Application.
- Controllers stay thin and only translate HTTP into Application requests.

The new read path should not reuse `CreateSubmissionCommand` or domain mutation logic. It should use query-specific result types shaped for the UI.

### Deferred Patterns

Domain events and outbox should wait until a feature needs reliable async side effects, such as "submission submitted" starting audit, notification, or underwriting workflows.

Idempotency should wait until repeated client retries could create duplicates in an important workflow, such as quote request or submit-for-review.

Strategy and adapter patterns should wait for premium calculation or external provider integration.

Cache-aside should wait until there is read-heavy or expensive data. Submission list/detail reads should use PostgreSQL directly for now.

## Planned File Structure

### Backend Application

- Modify: `src/LIAnsureProtect.Application/Submissions/ISubmissionRepository.cs`
  - Add read promises needed by Application queries.
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/ListSubmissions/ListSubmissionsQuery.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/ListSubmissions/ListSubmissionsQueryHandler.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/ListSubmissions/ListSubmissionsResult.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/GetSubmissionDetail/GetSubmissionDetailQuery.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/GetSubmissionDetail/GetSubmissionDetailQueryHandler.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/GetSubmissionDetail/SubmissionDetailResult.cs`

### Backend Infrastructure

- Modify: `src/LIAnsureProtect.Infrastructure/Submissions/EfCoreSubmissionRepository.cs`
  - Add `AsNoTracking()` read methods.
  - Project EF entities into query result DTOs.
  - Order list results by newest `CreatedAtUtc` first.

### Backend API

- Modify: `src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs`
  - Add `GET /api/v1/submissions`.
  - Add `GET /api/v1/submissions/{submissionId}`.
  - Keep authorization at the controller action boundary.
  - Return `404 Not Found` when detail is missing.

### Backend Tests

- Create: `tests/LIAnsureProtect.UnitTests/Submissions/ListSubmissions/ListSubmissionsQueryHandlerTests.cs`
- Create: `tests/LIAnsureProtect.UnitTests/Submissions/GetSubmissionDetail/GetSubmissionDetailQueryHandlerTests.cs`
- Modify: `tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs`
  - Add endpoint tests for anonymous list/detail rejection.
  - Add endpoint tests for successful list/detail reads.
  - Add endpoint test for missing detail returning `404 Not Found`.

### Frontend Feature Slice

- Modify: `src/LIAnsureProtect.Web/src/features/submissions/types.ts`
  - Add list item and detail response types.
- Create: `src/LIAnsureProtect.Web/src/features/submissions/api/listSubmissions.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/api/getSubmissionDetail.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/hooks/useSubmissions.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/hooks/useSubmissionDetail.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionsPage.tsx`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionDetailPage.tsx`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionsPage.test.tsx`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionDetailPage.test.tsx`

### Frontend Routing And Navigation

- Modify: `src/LIAnsureProtect.Web/src/App.tsx`
  - Add protected lazy routes for `/submissions` and `/submissions/:submissionId`.
- Modify: `src/LIAnsureProtect.Web/src/pages/DashboardPage.tsx`
  - Add "View submissions" navigation.
- Modify: `src/LIAnsureProtect.Web/src/pages/DashboardPage.test.tsx`
  - Assert dashboard links to both create and list workflows.

### Documentation

- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/project-status.md`
- Modify: `docs/architecture/overview.md`
- Create after implementation starts or closes: `docs/dev/milestone-10-submission-list-and-detail-foundation-learnings.md`

## Task 1: Document Milestone 10 Boundary

**Why:** The repo should say what Milestone 10 is before code changes begin. This prevents the slice from expanding into ownership, quote generation, outbox, cache, or external provider work too early.

**Files:**

- Modify: `docs/project-status.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Mark Milestone 10 as the current planned milestone**

Update `docs/project-status.md` so the current workspace points to:

```text
Current branch: `codex/milestone-10-submission-list-and-detail-foundation`
Current milestone: Milestone 10 - Submission List And Detail Foundation is planned.
```

- [ ] **Step 2: Add the approved Milestone 10 direction**

Add a new section after Milestone 9:

```markdown
### Milestone 10 - Submission List And Detail Foundation

Status: planned.

Approved direction:

- Add protected read endpoints for submission list and submission detail.
- Continue practical CQRS with MediatR by adding query handlers.
- Use REPR-style request/endpoint/response thinking without replacing the existing controller API.
- Keep the frontend inside the existing `src/features/submissions` vertical slice.
- Use TanStack Query for read loading, empty, error, success, and not-found states.
- Keep the milestone narrow and defer ownership, editing, questionnaire, documents, quotes, events, outbox, cache, external providers, API Gateway, and BFF.
```

- [ ] **Step 3: Verify documentation diff**

Run:

```powershell
git diff -- docs/project-status.md README.md CHANGELOG.md
```

Expected: only Milestone 10 planning text and links changed.

## Task 2: Add Application Query Contracts

**Why:** The Application layer should describe what the app can ask for without knowing EF Core or HTTP. The read side needs explicit query/result types.

**Files:**

- Modify: `src/LIAnsureProtect.Application/Submissions/ISubmissionRepository.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/ListSubmissions/ListSubmissionsQuery.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/ListSubmissions/ListSubmissionsQueryHandler.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/ListSubmissions/ListSubmissionsResult.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/GetSubmissionDetail/GetSubmissionDetailQuery.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/GetSubmissionDetail/GetSubmissionDetailQueryHandler.cs`
- Create: `src/LIAnsureProtect.Application/Submissions/Queries/GetSubmissionDetail/SubmissionDetailResult.cs`

- [ ] **Step 1: Write list query handler tests first**

Create tests proving the handler asks the repository for list results and returns them unchanged.

Expected behavior:

```text
ListSubmissionsQueryHandler
  -> calls ISubmissionRepository.ListAsync(cancellationToken)
  -> returns ListSubmissionsResult
```

- [ ] **Step 2: Write detail query handler tests first**

Create tests proving the handler asks the repository for one submission and returns null when the repository returns null.

Expected behavior:

```text
GetSubmissionDetailQueryHandler
  -> calls ISubmissionRepository.GetDetailAsync(submissionId, cancellationToken)
  -> returns SubmissionDetailResult?
```

- [ ] **Step 3: Add repository read promises**

Extend `ISubmissionRepository` with read methods similar to:

```csharp
Task<IReadOnlyList<SubmissionListItemResult>> ListAsync(CancellationToken cancellationToken);

Task<SubmissionDetailResult?> GetDetailAsync(
    Guid submissionId,
    CancellationToken cancellationToken);
```

Use result DTOs owned by the query feature rather than returning EF entities to the API.

- [ ] **Step 4: Add query/result/handler classes**

Use MediatR query records:

```csharp
public sealed record ListSubmissionsQuery : IRequest<ListSubmissionsResult>;
```

```csharp
public sealed record GetSubmissionDetailQuery(Guid SubmissionId)
    : IRequest<SubmissionDetailResult?>;
```

- [ ] **Step 5: Run focused unit tests**

Run:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --filter Submissions
```

Expected: new query handler tests pass with existing submission tests.

## Task 3: Add EF Core Read Repository Methods

**Why:** The existing EF Core repository only writes submissions. The read side needs efficient no-tracking queries for list and detail pages.

**Files:**

- Modify: `src/LIAnsureProtect.Infrastructure/Submissions/EfCoreSubmissionRepository.cs`

- [ ] **Step 1: Implement list read**

Use:

```csharp
dbContext.Submissions
    .AsNoTracking()
    .OrderByDescending(submission => submission.CreatedAtUtc)
```

Project only fields needed by the list page:

- submission id
- applicant name
- applicant email
- company name
- status
- created UTC timestamp

- [ ] **Step 2: Implement detail read**

Use:

```csharp
dbContext.Submissions
    .AsNoTracking()
    .Where(submission => submission.Id == submissionId)
```

Return `null` when no matching submission exists.

- [ ] **Step 3: Run build**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: build passes with 0 errors.

## Task 4: Add Protected API Read Endpoints

**Why:** The frontend cannot show submissions without HTTP read endpoints. The API should stay thin and delegate to MediatR.

**Files:**

- Modify: `src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs`
- Modify: `tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs`

- [ ] **Step 1: Add integration tests first**

Add tests for:

- anonymous `GET /api/v1/submissions` returns `401 Unauthorized`
- authorized `GET /api/v1/submissions` returns `200 OK`
- authorized list includes a previously saved submission
- anonymous `GET /api/v1/submissions/{id}` returns `401 Unauthorized`
- authorized `GET /api/v1/submissions/{id}` returns `200 OK`
- authorized missing detail returns `404 Not Found`

- [ ] **Step 2: Add controller actions**

Use action names similar to:

```csharp
[HttpGet]
[Authorize(Policy = ApplicationPolicies.CreateSubmission)]
public async Task<ActionResult<ListSubmissionsResult>> List(CancellationToken cancellationToken)
```

```csharp
[HttpGet("{submissionId:guid}")]
[Authorize(Policy = ApplicationPolicies.CreateSubmission)]
public async Task<ActionResult<SubmissionDetailResult>> GetById(
    Guid submissionId,
    CancellationToken cancellationToken)
```

For Milestone 10, reuse the existing `Submissions.Create` policy if no read policy exists yet. Note this as a conscious simplification and backlog a future `Submissions.Read` policy when fine-grained permissions are introduced.

- [ ] **Step 3: Run focused integration tests**

Run:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --filter SubmissionEndpointTests
```

Expected: submission endpoint tests pass.

## Task 5: Add Frontend Read API Functions And Hooks

**Why:** The frontend should keep API calls and Auth0 token usage inside the feature-owned submissions slice, matching Milestone 9.

**Files:**

- Modify: `src/LIAnsureProtect.Web/src/features/submissions/types.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/api/listSubmissions.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/api/getSubmissionDetail.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/hooks/useSubmissions.ts`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/hooks/useSubmissionDetail.ts`

- [ ] **Step 1: Add frontend types**

Add types similar to:

```ts
export type SubmissionListItem = {
  submissionId: string;
  applicantName: string;
  applicantEmail: string;
  companyName: string;
  status: string;
  createdAtUtc: string;
};

export type ListSubmissionsResponse = {
  submissions: SubmissionListItem[];
};

export type SubmissionDetailResponse = SubmissionListItem;
```

- [ ] **Step 2: Add fetch functions**

Follow the existing `createSubmission` style:

```text
listSubmissions(accessToken)
  -> GET /api/v1/submissions
  -> returns ListSubmissionsResponse
```

```text
getSubmissionDetail(accessToken, submissionId)
  -> GET /api/v1/submissions/{submissionId}
  -> returns SubmissionDetailResponse
```

For detail `404`, throw a clear error or return a typed not-found state. Pick one approach and keep tests aligned.

- [ ] **Step 3: Add TanStack Query hooks**

Use stable query keys:

```ts
["submissions"]
```

```ts
["submissions", submissionId]
```

The hooks should request the Auth0 access token silently, then call the feature API function.

## Task 6: Add Submission List Page

**Why:** Users need the first read workflow after creating submissions.

**Files:**

- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionsPage.tsx`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionsPage.test.tsx`

- [ ] **Step 1: Write frontend tests first**

Test these states:

- page heading renders
- loading state renders
- empty state renders when there are no submissions
- error state renders when API call fails
- successful list renders applicant/company/status
- each row links to `/submissions/{submissionId}`
- create-submission link goes to `/submissions/new`

- [ ] **Step 2: Implement page**

The page should:

- use `useSubmissions`
- show a compact list/table-like layout
- include a "Create submission" link
- include a "Back to dashboard" link
- avoid adding ownership filters or advanced search in this milestone

- [ ] **Step 3: Run focused frontend test**

Run from `src/LIAnsureProtect.Web`:

```powershell
npm test -- SubmissionsPage
```

If `npm` is not available on the Codex shell PATH, use the bundled Codex Node/npm runtime as already noted in Milestone 9.

## Task 7: Add Submission Detail Page

**Why:** A list is useful only if users can inspect one submission after selecting it.

**Files:**

- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionDetailPage.tsx`
- Create: `src/LIAnsureProtect.Web/src/features/submissions/pages/SubmissionDetailPage.test.tsx`

- [ ] **Step 1: Write frontend tests first**

Test these states:

- page obtains `submissionId` from route params
- loading state renders
- error state renders
- not-found state renders if the API reports missing detail
- successful detail renders applicant name, email, company, status, created timestamp, and submission id
- page links back to `/submissions`

- [ ] **Step 2: Implement page**

Use `useParams` from React Router and the `useSubmissionDetail` hook.

Keep the page read-only. Do not add edit, submit, withdraw, document upload, or quote actions in this milestone.

- [ ] **Step 3: Run focused frontend test**

Run from `src/LIAnsureProtect.Web`:

```powershell
npm test -- SubmissionDetailPage
```

Expected: detail page tests pass.

## Task 8: Wire Routes And Dashboard Navigation

**Why:** Users should discover the new read workflow from the existing signed-in dashboard.

**Files:**

- Modify: `src/LIAnsureProtect.Web/src/App.tsx`
- Modify: `src/LIAnsureProtect.Web/src/pages/DashboardPage.tsx`
- Modify: `src/LIAnsureProtect.Web/src/pages/DashboardPage.test.tsx`

- [ ] **Step 1: Add lazy routes**

Add protected routes:

```text
/submissions
/submissions/:submissionId
```

Keep route-level code splitting with `React.lazy` and `Suspense`.

- [ ] **Step 2: Update dashboard navigation**

Add a "View submissions" link near "Create submission".

The dashboard should become a workflow hub:

```text
Dashboard
  -> View submissions
  -> Create submission
```

- [ ] **Step 3: Update dashboard tests**

Assert both links exist:

```text
href="/submissions"
href="/submissions/new"
```

## Task 9: Full Verification

**Why:** This milestone crosses backend, database, frontend routing, Auth0 token usage, and docs. Focused tests are not enough at closeout.

**Files:**

- No direct file edits unless verification reveals a defect.

- [ ] **Step 1: Run backend tests**

```powershell
dotnet test LIAnsureProtect.slnx --no-build
```

Expected: UnitTests and IntegrationTests pass.

- [ ] **Step 2: Run frontend checks**

From `src/LIAnsureProtect.Web`:

```powershell
npm run build
npm run lint
npm test
```

Expected: TypeScript build, ESLint, and Vitest pass.

- [ ] **Step 3: Run local CI**

From repo root:

```powershell
.\scripts\run-local-ci.ps1
```

Expected: backend setup/tests/smoke checks and frontend checks pass, and a `TestResults` artifact is created.

- [ ] **Step 4: Manual browser smoke**

With API and frontend running:

1. Sign in through Auth0.
2. Open `/submissions/new`.
3. Create a draft submission.
4. Open `/submissions`.
5. Confirm the new submission appears.
6. Open the detail link.
7. Confirm applicant, company, status, id, and timestamp display correctly.
8. Optionally confirm the row exists in PostgreSQL.

## Task 10: Close Milestone 10 Documentation

**Why:** The project relies on docs as the continuity checkpoint between sessions.

**Files:**

- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/project-status.md`
- Modify: `docs/architecture/overview.md`
- Create or update: `docs/dev/milestone-10-submission-list-and-detail-foundation-learnings.md`

- [ ] **Step 1: Update status and README**

Record that Milestone 10 added protected list/detail read workflows.

- [ ] **Step 2: Update architecture overview**

Add the read flow:

```text
GET /api/v1/submissions
  -> SubmissionsController
  -> ListSubmissionsQuery
  -> ListSubmissionsQueryHandler
  -> ISubmissionRepository.ListAsync(...)
  -> EfCoreSubmissionRepository
  -> SubmissionDbContext.Submissions.AsNoTracking()
  -> PostgreSQL
```

```text
GET /api/v1/submissions/{submissionId}
  -> SubmissionsController
  -> GetSubmissionDetailQuery
  -> GetSubmissionDetailQueryHandler
  -> ISubmissionRepository.GetDetailAsync(...)
  -> EfCoreSubmissionRepository
  -> SubmissionDbContext.Submissions.AsNoTracking()
  -> PostgreSQL
```

- [ ] **Step 3: Write learning notes**

Capture:

- why read queries are separate from create commands
- why no-tracking EF Core reads are appropriate
- why REPR thinking was used without rewriting controllers
- why ownership, outbox, idempotency, cache, gateway, and BFF were deferred
- backend and frontend verification commands
- browser smoke-test results
- any debugging issues encountered

- [ ] **Step 4: Commit closeout**

After verification passes:

```powershell
git status --short
git diff --stat
git add -- README.md CHANGELOG.md docs src tests
git commit -m "feat: add submission list and detail foundation"
```

## Noteworthy Decisions To Preserve

- Milestone 10 is the read-side counterpart to Milestone 9's create workflow.
- The project should continue controller-based APIs for now. REPR is used as a design lens, not a framework rewrite.
- The frontend should remain feature-owned under `src/features/submissions`.
- The backend should continue practical CQRS with MediatR.
- PostgreSQL remains the system of record. No separate read database is needed.
- EF Core read methods should use `AsNoTracking()` unless a future feature needs tracked entities.
- A future `Submissions.Read` policy is likely, but Milestone 10 can reuse the existing submission policy to keep the slice small if that is explicitly documented.
- Ownership filtering is important later but intentionally deferred until the project has internal user/profile ownership data.
- Domain events and outbox remain future work for async side effects, not this read milestone.
- Cache-aside remains future work for read-heavy or expensive data, not basic submission list/detail reads.
- API Gateway and BFF are deployment/frontend-composition decisions and should wait until the cloud shape or frontend complexity justifies them.
