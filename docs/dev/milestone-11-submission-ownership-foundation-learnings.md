# Milestone 11 - Submission Ownership Foundation Learnings

This document records the implementation notes, security boundary decisions, EF Core query decisions, tests, and verification results for `Milestone 11 - Submission Ownership Foundation`.

Milestone 10 let any signed-in user with an allowed role read every submission. That was acceptable for the first read workflow, but it was not a safe long-term authorization boundary. Milestone 11 adds the first real ownership rule so submission list/detail reads are scoped to the signed-in user.

## Goal

The goal is not to add the full customer, broker, organization, underwriter, or admin ownership model yet.

The goal is this first safe rule:

```text
The user who creates a submission owns that submission.
When that user lists or opens submissions, the API only returns rows owned by that same user id.
```

Simple analogy:

```text
Milestone 10:
  Every signed-in person with the right badge could open the whole submissions filing cabinet.

Milestone 11:
  The badge still gets the user into the submissions room,
  but each drawer is now labeled with an owner id.
  The user can only open drawers with their own label.
```

## Approved Scope

In scope:

- Add owner metadata to the `Submission` aggregate.
- Persist that owner metadata in PostgreSQL.
- Stamp new submissions with the authenticated user id from `ICurrentUser.UserId`.
- Scope submission list reads to the authenticated user id.
- Scope submission detail reads to both submission id and authenticated user id.
- Return `404 Not Found` when a user asks for a submission that exists but belongs to someone else.
- Add `Submissions.Read` so read endpoints no longer reuse the create policy name.
- Add focused unit and integration tests for ownership behavior.

Out of scope:

- Organization/team ownership.
- Broker books of business.
- Underwriter assignment.
- Admin bypass for all submissions.
- Questionnaires.
- Document uploads.
- Underwriting notes.
- Quote generation.
- External provider adapters.
- API Gateway.
- BFF.
- Lazy loading.

## Files Added Or Updated

Domain:

```text
src/LIAnsureProtect.Domain/Submissions/Submission.cs
```

Application:

```text
src/LIAnsureProtect.Application/Common/Security/ApplicationPolicies.cs
src/LIAnsureProtect.Application/Submissions/ISubmissionRepository.cs
src/LIAnsureProtect.Application/Submissions/Commands/CreateSubmission/CreateSubmissionCommandHandler.cs
src/LIAnsureProtect.Application/Submissions/Queries/ListSubmissions/ListSubmissionsQueryHandler.cs
src/LIAnsureProtect.Application/Submissions/Queries/GetSubmissionDetail/GetSubmissionDetailQueryHandler.cs
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260619100855_AddSubmissionOwnership.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260619100855_AddSubmissionOwnership.Designer.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/SubmissionDbContextModelSnapshot.cs
src/LIAnsureProtect.Infrastructure/Submissions/EfCoreSubmissionRepository.cs
```

API:

```text
src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs
src/LIAnsureProtect.Api/Security/AuthorizationPolicies.cs
```

Tests:

```text
tests/LIAnsureProtect.UnitTests/Submissions/TestCurrentUser.cs
tests/LIAnsureProtect.UnitTests/Submissions/SubmissionTests.cs
tests/LIAnsureProtect.UnitTests/Submissions/CreateSubmission/CreateSubmissionCommandHandlerTests.cs
tests/LIAnsureProtect.UnitTests/Submissions/ListSubmissions/ListSubmissionsQueryHandlerTests.cs
tests/LIAnsureProtect.UnitTests/Submissions/GetSubmissionDetail/GetSubmissionDetailQueryHandlerTests.cs
tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs
tests/LIAnsureProtect.IntegrationTests/PostgreSqlPersistenceTests.cs
```

Documentation:

```text
README.md
CHANGELOG.md
docs/architecture/overview.md
docs/project-status.md
docs/dev/milestone-9-submission-intake-ui-foundation-learnings.md
docs/dev/milestone-11-submission-ownership-foundation-learnings.md
```

## Ownership Id Decision

Milestone 11 uses `ICurrentUser.UserId` as the owner value.

In local integration tests, that value comes from:

```text
X-Test-UserId
```

In real Auth0-backed browser use, that value is expected to come from the JWT subject claim:

```text
sub
```

Why not use email:

- Email addresses can change.
- Email addresses are contact data, not a stable identity key.
- The identity provider subject is designed to be the stable user identifier inside tokens.

Simple rule:

```text
Use email to contact a person.
Use user id to own data.
```

## Create Flow

The create flow now stamps ownership before saving:

```text
POST /api/v1/submissions
  -> Submissions.Create authorization policy
  -> CreateSubmissionCommandHandler
  -> ICurrentUser.UserId
  -> Submission.CreateDraft(..., ownerUserId, ...)
  -> ISubmissionRepository.AddAsync(...)
  -> IUnitOfWork.SaveChangesAsync(...)
  -> PostgreSQL owner_user_id
```

If the current user id is missing, the Application handler throws an `InvalidOperationException`. In normal API use, the endpoint is protected and real access tokens should contain a subject. In tests, this makes a broken test principal fail loudly instead of saving ownerless submissions.

## Read Flow

The read endpoints now use a separate read policy:

```text
Submissions.Read
```

For now, `Submissions.Read` allows the same broad roles as `Submissions.Create`:

```text
Customer
Broker
Admin
```

This milestone does not give Admin a global bypass. Even Admin is still owner-scoped for submission list/detail reads until a later milestone explicitly designs an admin/back-office view.

List flow:

```text
GET /api/v1/submissions
  -> Submissions.Read authorization policy
  -> ListSubmissionsQueryHandler
  -> ICurrentUser.UserId
  -> ISubmissionRepository.ListAsync(ownerUserId, ...)
  -> Where(submission => submission.OwnerUserId == ownerUserId)
  -> OrderByDescending(submission => submission.CreatedAtUtc)
  -> Select(...)
```

Detail flow:

```text
GET /api/v1/submissions/{submissionId}
  -> Submissions.Read authorization policy
  -> GetSubmissionDetailQueryHandler
  -> ICurrentUser.UserId
  -> ISubmissionRepository.GetDetailAsync(submissionId, ownerUserId, ...)
  -> Where(submission => submission.Id == submissionId)
  -> Where(submission => submission.OwnerUserId == ownerUserId)
  -> Select(...)
```

## Why Cross-Owner Detail Returns 404

If user A asks for user B's submission id, the API returns:

```text
404 Not Found
```

It does not return:

```text
403 Forbidden
```

Why:

- `403 Forbidden` can reveal that the submission id exists but belongs to someone else.
- `404 Not Found` keeps the response the same as a genuinely missing id.
- This avoids leaking record existence across users.

Simple rule:

```text
For private per-user records, "not yours" can safely look like "not found."
```

## EF Core Query Decision

Milestone 11 uses explicit repository filters:

```csharp
.Where(submission => submission.OwnerUserId == ownerUserId)
```

It does not use a global query filter:

```csharp
HasQueryFilter(...)
```

Why explicit filters are better for this milestone:

- The project has one owned aggregate so far.
- The ownership model is still intentionally small.
- Passing `ownerUserId` into repository methods makes the security rule visible in tests and code review.
- A global filter can be useful later, but it can also hide important query behavior if added too early.

Simple analogy:

```text
Explicit filter:
  The repository method clearly says,
  "Only pull folders for this owner."

Global query filter:
  The filing cabinet silently applies that rule everywhere.
```

Silent rules can be helpful after the model is mature. Early in a learning project, visible rules are easier to understand and verify.

## Database Migration

The migration adds:

```text
owner_user_id
```

to:

```text
public.submissions
```

It also adds this index:

```text
ix_submissions_owner_user_id_created_at_utc
```

Why the index starts with owner id:

```text
The list query first filters by owner,
then orders the owner's submissions by created time.
```

Existing local rows need a value when the non-null column is added. The migration backfills existing rows with:

```text
legacy-unassigned
```

This is clearer than an empty string. The value is a migration backfill, not the normal owner for new rows. New rows created through the API should always use the authenticated user id.

## TDD Notes

The RED unit test run failed before implementation because the expected ownership API did not exist yet.

Command:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore
```

Expected missing behavior:

```text
Submission.CreateDraft(...) did not accept ownerUserId.
Submission did not expose OwnerUserId.
ISubmissionRepository.ListAsync(...) did not accept ownerUserId.
ISubmissionRepository.GetDetailAsync(...) did not accept ownerUserId.
Query handlers did not accept ICurrentUser.
CreateSubmissionCommandHandler did not accept ICurrentUser.
```

The GREEN unit test run passed after implementation:

```text
Passed: 17
Skipped: 0
Failed: 0
```

Integration tests then proved the request-pipeline behavior:

- create saves `OwnerUserId`
- list returns only submissions owned by the current test user
- detail returns `404 Not Found` for a submission owned by another user

## Verification

Focused unit tests:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore
```

Result:

```text
Passed: 17
Skipped: 0
Failed: 0
```

Focused integration tests:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

First attempt:

```text
Failed because LIAnsureProtect.Api.exe was still running and locking API output DLLs.
```

Fix:

```powershell
Stop-Process -Id 28440 -Force
```

Second attempt:

```text
Passed: 19
Skipped: 1 PostgreSQL opt-in test
Failed: 0
```

Backend solution test:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
```

Result:

```text
UnitTests: 17 passed
IntegrationTests: 19 passed, 1 skipped PostgreSQL opt-in test
```

Full local CI:

```powershell
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

This Codex shell still does not expose normal `npm` or `node` commands on `PATH`, so the run used the bundled package runner as a temporary `npm` shim and skipped frontend install because `node_modules` already existed.

Result:

```text
Local CI passed.
Artifact zip: TestResults\local-ci-20260619-185327.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- Migration `20260611012509_CreateSubmissionPersistence` applied.
- Migration `20260619100855_AddSubmissionOwnership` applied.
- Backend build passed with 0 warnings and 0 errors.
- UnitTests passed: 17.
- IntegrationTests passed: 20, including the PostgreSQL opt-in persistence test inside the CI environment.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed: 5 files, 16 tests.
- The CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.

## What To Remember

- `Submissions.Read` now protects read endpoints.
- `Submissions.Create` still protects create.
- Role authorization decides whether the user may enter the submissions read/create workflow.
- Ownership filtering decides which rows that authenticated user may see.
- `OwnerUserId` is intentionally simple and user-based for now.
- Organization, broker, underwriter, and admin ownership models are still later design work.
- `404 Not Found` for cross-owner detail reads avoids leaking whether another user's submission id exists.
- Explicit repository filters are preferred for this first ownership milestone because they keep the rule visible.
