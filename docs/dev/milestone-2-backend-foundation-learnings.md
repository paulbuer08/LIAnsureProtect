# Milestone 2 Backend Foundation Learnings

This document records the practical decisions made while creating the LIAnsureProtect backend foundation. It is intentionally written in simple English so the same reasoning can be reused in future projects.

## Why This Document Exists

Milestone 2 was not only about creating projects. It also surfaced real production-minded questions:

- How should a solution be organized?
- What belongs in `src` versus `tests`?
- Why use Clean Architecture references in a specific direction?
- What does OpenAPI actually do?
- Why add ProblemDetails and health checks early?
- How should logging work locally, in tests, and later in AWS?
- How do integration tests start the API?
- When should values be constants, config, or shared abstractions?

The answers below are the current project decisions. They are not permanent laws. If a later milestone proves a decision is wrong or too early, update this document and explain why the plan changed.

## Solution And Folder Structure

The solution file is the Visual Studio table of contents for the backend. It tells Visual Studio which projects belong together.

The project uses the newer `.slnx` solution format:

```text
LIAnsureProtect.slnx
```

That is acceptable for this .NET 10 project.

Real folders on disk are different from Visual Studio solution folders:

- A real folder exists in File Explorer.
- A solution folder is mainly a Visual Studio grouping.

The project uses both names consistently:

```text
src/
  LIAnsureProtect.Domain/
  LIAnsureProtect.Application/
  LIAnsureProtect.Infrastructure/
  LIAnsureProtect.Api/
  LIAnsureProtect.Worker/
tests/
  LIAnsureProtect.UnitTests/
  LIAnsureProtect.IntegrationTests/
```

`src` contains production code. It is the code that can eventually be shipped.

`tests` contains verification code. It proves the production code behaves correctly.

This separation matters because it keeps the repo easy to scan and makes CI/CD easier later.

## Visual Studio `.vs` Folder

Visual Studio may create a `.vs` folder. This is normal.

It stores local IDE state such as:

- window layout
- temporary solution state
- debug settings
- IntelliSense cache

It should not be committed to Git. If Visual Studio gets confused later, deleting `.vs` is usually safe because Visual Studio can recreate it.

## Clean Architecture Project References

The intended dependency direction is:

```text
Domain
  no references

Application
  references Domain

Infrastructure
  references Application
  references Domain

Api
  references Application
  references Infrastructure

Worker
  references Application
  references Infrastructure

UnitTests
  references Domain
  references Application

IntegrationTests
  references Api
```

The rule is that inner layers should not depend on outer layers.

Simple analogy:

- `Domain` is the rulebook.
- `Application` is the person using the rulebook to perform a task.
- `Infrastructure` is the toolbox that knows how to talk to databases, queues, files, and cloud services.
- `Api` is the front desk that receives HTTP requests.
- `Worker` is the back office that handles background work.

`Infrastructure` can reference `Domain` directly because persistence code often needs to save and load domain objects. The workflow still goes through Application. That means the API should call Application use cases, and Infrastructure should implement Application interfaces.

## Why There Is No `Persistence` Project Yet

A separate `Persistence` project can be useful in some systems, but this project starts with `Infrastructure`.

Reason:

- Milestone 2 has no database schema yet.
- EF Core, PostgreSQL, Redis, S3, messaging, and other outside services all belong to the broad Infrastructure layer for now.
- If persistence grows large enough later, it can be split with a real reason.

Do not add a separate persistence boundary just for ceremony.

## API Template Cleanup

The ASP.NET Core Web API template created weather forecast demo files. Those were removed because they are tutorial sample code, not LIAnsureProtect behavior.

Class library template files such as `Class1.cs` were also removed. Empty template files make the repo look unfinished and do not teach anything useful.

## Controllers And Minimal Endpoints

The API project uses controllers for business endpoints.

Controllers are a good fit because the app will eventually have many business areas:

- submissions
- quotes
- policies
- claims
- documents
- users
- admin workflows

Small `MapGet` endpoints are still allowed for simple infrastructure endpoints. For example, the root status endpoint is small enough that a controller would add more ceremony than value.

Current direction:

```text
Business endpoints:
  controllers

Small status/infrastructure endpoints:
  MapGet is acceptable
```

## OpenAPI

OpenAPI is the machine-readable API contract. It describes endpoints, request bodies, response bodies, status codes, and schemas.

Swagger UI is a browser UI commonly used to view or test OpenAPI documents. The built-in ASP.NET Core OpenAPI package generates documents but does not automatically mean the full Swagger UI experience is installed.

The `.http` file in Visual Studio is different. It is a local request scratchpad, like a small built-in Postman file.

Current decision:

- Generate OpenAPI documents in development.
- Do not expose OpenAPI publicly in production yet.

Later direction:

- Add formal API versioning before breaking API changes or multiple live versions.
- Generate separate OpenAPI documents if needed for v1/v2, public/internal APIs, or frontend/backend groupings.
- Protect OpenAPI with role-based authorization before exposing it outside local development.

## API Versioning

The project starts public business routes under:

```text
/api/v1/...
```

This makes the first public contract explicit.

Example future shape:

```text
/api/v1/submissions
/api/v2/submissions
```

Do not create fake v2 or v3 endpoints before they exist. Versioning should protect real consumers from real breaking changes, not decorate the app prematurely.

## OpenAPI Security, Audiences, And Caching

OpenAPI can reveal useful details about internal endpoints. That is helpful for developers but risky if exposed carelessly.

Current plan:

- Keep OpenAPI development-only.

Later options:

- Require authentication to view docs.
- Restrict docs to Admin or Developer roles.
- Produce separate public and internal OpenAPI documents.
- Produce separate versioned documents such as v1 and v2.

Caching OpenAPI is reasonable later because OpenAPI documents rarely change at runtime. However, protected/internal API metadata should not be cached publicly.

## ProblemDetails

ProblemDetails gives API errors a standard shape.

Without it, errors often become inconsistent:

```json
{ "error": "Bad request" }
```

or:

```json
{ "message": "Something failed" }
```

ProblemDetails is more like a standard incident report:

```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "The request was invalid.",
  "instance": "/api/v1/submissions"
}
```

Why it matters:

- The frontend can handle errors consistently.
- API clients get a predictable shape.
- Tests can assert standard error behavior.
- Production errors avoid leaking stack traces or internal details.

ProblemDetails does not replace business validation. Later, Application-level validation and domain errors should be mapped into ProblemDetails responses.

## Health Checks

Health checks let systems ask whether the app is alive or ready.

Current endpoint:

```text
/api/v1/health
```

Simple analogy: it is like checking whether a store is open before sending customers inside.

Production systems can use health checks for:

- load balancers
- ECS/Fargate
- Kubernetes
- uptime monitoring
- deployment checks

Later, the app may need separate health checks:

```text
/health/live
/health/ready
```

Liveness means the process is alive.

Readiness means the app can safely receive traffic, including important dependencies such as PostgreSQL, Redis, queues, and storage.

Do not expose detailed dependency failure information publicly unless it is protected.

## HTTPS Redirection And Local Certificates

`UseHttpsRedirection` redirects HTTP requests to HTTPS.

Example:

```text
http://localhost:5000/api/v1/health
```

redirects to:

```text
https://localhost:5001/api/v1/health
```

Why it matters:

- HTTPS encrypts traffic.
- Insurance data, documents, claims, and auth tokens must not travel in plain text.
- The app should build secure habits early.

Visual Studio may ask to trust the ASP.NET Core development certificate. That is normal for local development. It does not create a production certificate.

Later, when running behind CloudFront, ALB, or API Gateway, the app may also need forwarded headers so ASP.NET Core understands the original public request scheme and host.

## Exception Handling

`UseExceptionHandler` is built-in ASP.NET Core middleware.

Middleware is a checkpoint that requests pass through.

If an unhandled exception happens later in the pipeline, this middleware can return a controlled error response instead of leaking internal details.

Current decision:

- In Development, expose developer-friendly diagnostics.
- Outside Development, use the exception handler.

Later, custom exception handling can map application exceptions to specific HTTP responses.

Examples:

- validation error -> 400 Bad Request
- missing resource -> 404 Not Found
- unauthorized user -> 401 Unauthorized
- forbidden action -> 403 Forbidden

## Logging

`WebApplication.CreateBuilder(args)` configures built-in ASP.NET Core logging.

Logs may appear in different places depending on how the app runs:

- Visual Studio Output window
- terminal when using `dotnet run`
- container stdout/stderr
- CloudWatch Logs when ECS/Fargate collects container logs

Current decision:

- Use built-in logging first.
- Do not add Serilog yet.

Why not Serilog immediately:

- It is a good tool, but it is an extra dependency.
- Milestone 2 only needs a clean baseline.
- ECS/Fargate can collect console logs into CloudWatch without app-specific CloudWatch logging code.

Add Serilog later if the app needs:

- richer structured log formatting
- correlation IDs and enrichment
- file logging
- custom sinks
- more control over production log output

## Integration Testing

Integration tests check that larger pieces work together.

The first real integration tests call:

```text
/
/api/v1/health
```

It uses `WebApplicationFactory<Program>`, which starts the API in memory. This means the test does not need a browser or a separately launched API process.

Simple analogy: the test creates a small temporary copy of the API just for the test, calls it, and throws it away.

The tests assert:

- the root endpoint returns `200 OK`
- the root endpoint returns the API application name and `Running` status
- the health endpoint returns `200 OK`
- the health endpoint response body is `Healthy`

## xUnit v3

The project moved to `xunit.v3` because the older `xunit` package showed a deprecation warning.

Starting with the non-deprecated test package avoids carrying known technical debt into a new project.

## Fact Versus Theory

Use `[Fact]` when one test checks one behavior with no input variations.

Example:

```text
Health endpoint returns success.
```

Use `[Theory]` when the same test logic should run with multiple inputs.

Examples later:

- invalid premium amounts
- different user roles
- multiple API versions
- multiple allowed document types

Current health test uses `[Fact]` because only one health endpoint exists.

## Arrange, Act, Assert

Tests should be easy to read:

```text
Arrange:
  prepare input and setup

Act:
  perform the behavior being tested

Assert:
  verify the result
```

For the health endpoint:

```text
Arrange:
  choose /api/v1/health

Act:
  send GET request

Assert:
  response is 200 OK and body is Healthy
```

This structure makes the test readable even for someone new to the codebase.

## Test Route Constants

The health test uses a test-local route constant rather than repeating a string everywhere.

This removes noise while still keeping the public URL visible in the test.

Do not over-share route constants between the API implementation and contract tests too early. If the API and test use the same shared route constant, both can change together and the test may stop protecting the public contract.

Rule:

- Use local constants in tests for public contract expectations.
- Consider shared route helpers later for implementation organization.

## Test Base Address

The test uses:

```text
https://localhost
```

This is not the real deployed API URL. It is the in-memory test server base address.

Because it is test infrastructure, keep it in the test or a future shared test factory, not in app configuration.

Use config for values that change by environment:

- database connection strings
- AWS region
- external API base URLs
- feature flags

Use constants or static readonly fields for test structure:

- test server base address
- expected test route
- expected test response text

## Auto Redirect In Tests

The integration test sets:

```text
AllowAutoRedirect = false
```

Why:

- It prevents `HttpClient` from hiding redirects.
- It helps catch route or HTTPS mistakes.
- It will matter more when authentication exists, because redirects can hide 401, 403, or login redirect behavior.

For endpoint contract tests, seeing the first actual response is usually better than following redirects automatically.

## Test Logging

The integration test clears logging providers in the test host.

Reason:

- Some machine-specific providers, such as Windows EventLog, can fail in restricted environments.
- Tests should be deterministic and not depend on local machine permissions.
- Quiet tests are easier to read.

Do not add console logging to every test by default. Add it temporarily only when debugging a failing test.

This affects only the test host, not production logging.

## Breakpoints In Tests

Visual Studio can debug tests.

Useful variables to inspect:

- request URI
- response
- status code
- response content

This is useful for learning because integration tests can be stepped through like normal code.

## Unit Test Project Is Empty For Now

`LIAnsureProtect.UnitTests` currently has no tests.

That is intentional for this point in Milestone 2. There is no real Domain or Application behavior yet, so adding fake unit tests would create noise.

Add unit tests when there is real pure logic to test, such as:

- value object validation
- premium calculation rules
- submission status transitions
- application use case behavior

## Current Production Middleware Backlog

Do not add all production middleware at once. Add middleware when its supporting feature exists.

Planned later:

- Authentication when identity exists.
- Authorization policies when roles and protected workflows exist.
- CORS when the React frontend runs on a different origin.
- Forwarded headers when behind CloudFront, ALB, API Gateway, or another reverse proxy.
- HSTS when production HTTPS hosting is finalized.
- Rate limiting for public API protection.
- Response compression for payload efficiency.
- Safe output caching only for non-sensitive responses.
- Request correlation and tracing for production observability.
- Readiness health checks after databases, queues, caches, and storage are added.

This keeps Milestone 2 clean while preserving the production roadmap.

## Current Verification Standard

Before considering Milestone 2 stable, run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
git diff --check
```

Expected current result:

- build succeeds with 0 warnings and 0 errors
- integration test passes
- unit test project builds but has no tests yet
- `git diff --check` passes, aside from normal CRLF warnings on Windows

## Next Useful Backend Steps

After this foundation is stable:

1. Add shared application/infrastructure dependency registration extension methods.
2. Decide whether Milestone 2 should include a small architecture-boundary test, or leave unit tests empty until business logic exists.
3. Commit Milestone 2 when docs, build, and tests are stable.

Do not add auth, database schema, React frontend, or cloud infrastructure until the relevant milestone.
