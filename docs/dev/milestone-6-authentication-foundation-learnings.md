# Milestone 6 - Authentication Foundation Learnings

This document records the decisions and tradeoffs from Milestone 6 - Authentication Foundation.

## Why This Milestone Exists

Milestone 5 made submissions persistent in PostgreSQL. The next security step is making sure business endpoints are not open to anonymous callers.

Simple analogy:

```text
Milestone 5:
  Build the filing cabinet.

Milestone 6:
  Add the front-desk badge check before someone can file paperwork.
```

Authentication answers:

```text
Who are you?
```

Authorization answers:

```text
What are you allowed to do?
```

For LIAnsureProtect, that means:

```text
Authentication:
  Is this a real logged-in caller?

Authorization:
  Can this caller create a submission?
```

## Authentication Direction

Use standards-based JWT bearer authentication for the API.

The API should validate access tokens issued by an external OpenID Connect/OAuth identity provider. The API should not create production access tokens itself and should not build custom password or token logic.

The recommended provider direction is Auth0 by Okta for the first external identity provider because it gives a strong CIAM developer experience and supports modern OIDC/OAuth flows, MFA, passwordless options, organizations, RBAC, and attack protection.

Keep the API provider-neutral:

```text
Auth0 today
  -> standard JWT/OIDC claims
  -> ASP.NET Core JwtBearer validation
  -> Application roles and policies
```

If a later milestone switches to Amazon Cognito, Microsoft Entra External ID, or another standards-based provider, the Application layer should not need to change.

## JWT Access Tokens

A JWT access token is like a signed digital badge.

The API validates:

- issuer: who created the badge
- audience: which API the badge is for
- signature: whether the badge was tampered with
- expiration: whether the badge is still valid
- role claim type: where roles are stored in the token

If the API does not know which issuer and audience to trust, it should fail at startup instead of running with unclear security.

## Configuration Direction

`appsettings.json` contains the safe configuration shape:

```json
"Authentication": {
  "Authority": "",
  "Audience": "",
  "RoleClaimType": "roles"
}
```

Development values can live in `appsettings.Development.json` while the project is local.

Production values should come from deployment configuration such as environment variables, AWS Systems Manager Parameter Store, or AWS Secrets Manager as appropriate:

```text
Authentication__Authority=https://prod-auth-domain/
Authentication__Audience=https://api.liansureprotect.com
Authentication__RoleClaimType=roles
```

The double underscore maps to nested ASP.NET Core configuration:

```text
Authentication__Authority
  -> Authentication:Authority
```

The current JWT validation values are generally not secrets:

- `Authority` is the trusted issuer URL.
- `Audience` is the API identifier.
- `RoleClaimType` is the claim name that contains roles.

Real secrets such as client secrets, database passwords, API keys, and signing private keys must not be committed to source control.

## Current User Abstraction

`ICurrentUser` belongs in the Application layer because use cases eventually need to ask who is making a request without depending on ASP.NET Core.

Simple analogy:

```text
Application:
  "Tell me who is holding the badge."

API:
  "I can inspect the HTTP request and answer that."
```

`HttpContextCurrentUser` lives in the API layer because it reads `HttpContext.User`, which is an ASP.NET Core detail.

`GetRoles()` is a method instead of a property because it gathers role claims from the current user. A method better communicates that work is being performed.

## Roles And Policies

Roles are broad job categories:

```text
Customer
Broker
Underwriter
ClaimsAdjuster
Admin
```

Policies are specific permissions:

```text
Submissions.Create
System.Admin
```

Simple rule:

```text
Role:
  Who you are.

Policy:
  Which door you can open.
```

The first protected business endpoint is:

```text
POST /api/v1/submissions
```

It requires the `Submissions.Create` policy.

Allowed roles:

```text
Customer
Broker
Admin
```

`Underwriter` is intentionally not allowed to create submissions. Underwriting review should be a separate workflow.

## Middleware Order

Authentication must run before authorization:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Simple analogy:

```text
UseAuthentication:
  Read the badge.

UseAuthorization:
  Check whether the badge can enter the room.
```

## Protected Endpoint Response Attribute Order

For protected API endpoints, list response metadata in gate order:

1. `401 Unauthorized` when the caller is not authenticated.
2. `403 Forbidden` when the caller is authenticated but not authorized.
3. `400 Bad Request` or other validation/input errors.
4. Success response such as `200 OK`, `201 Created`, or `204 No Content`.

Example:

```csharp
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<CreateSubmissionResult>(StatusCodes.Status201Created)]
```

This order mirrors the real request flow:

```text
security gate first
  -> input validation second
  -> successful business behavior last
```

Use this convention for future protected endpoints.

## Validation Versus Security Failures

Do not put `401 Unauthorized` or `403 Forbidden` into `ApplicationValidationException`.

Those failures are different:

```text
401 Unauthorized:
  No valid login.

403 Forbidden:
  Logged in, but not allowed.

400 Bad Request:
  Request body is invalid.
```

Analogy:

```text
401/403:
  The building guard stops you at the front door.

400:
  You reached the counter, but your form is filled out incorrectly.
```

Authentication and authorization happen before the controller action reaches Application validation.

## Test Authentication

Integration tests use a test-only authentication handler.

Production uses real JWT bearer authentication. Tests use clearly named headers:

```text
X-Test-UserId
X-Test-Email
X-Test-Roles
```

This lets tests cover different security cases without needing real Auth0 tokens:

```text
No headers
  -> anonymous
  -> 401 Unauthorized

X-Test-Roles: Underwriter
  -> authenticated but not allowed
  -> 403 Forbidden

X-Test-Roles: Customer
  -> authenticated and allowed
  -> validation or success path
```

The test handler must stay in the integration test project only. It must never be registered in production code.

`Task.FromResult(...)` is acceptable in the test authentication handler because `HandleAuthenticateAsync()` must return `Task<AuthenticateResult>`, but the test handler only reads headers and builds claims synchronously. Avoid fake async work such as `Task.Delay(...)`.

## Test Coverage Added

The submission endpoint tests now cover the full gate sequence:

```text
anonymous caller
  -> 401 Unauthorized

authenticated Underwriter
  -> 403 Forbidden

authenticated Customer with invalid input
  -> 400 Bad Request

authenticated Customer with valid input
  -> 201 Created

authorized roles Customer, Broker, and Admin
  -> 201 Created
```

Root and health endpoint tests confirm these operational endpoints remain anonymous:

```text
GET /
GET /api/v1/health
```

## Local CI Smoke Test Direction

`run-local-ci.ps1` should not anonymously create a submission anymore.

After this milestone, local CI smoke testing should verify:

```text
GET /
  -> 200 OK

GET /api/v1/health
  -> Healthy

anonymous POST /api/v1/submissions
  -> 401 Unauthorized
```

An authenticated smoke test can be added later after the project has real dev identity provider setup or a deliberate dev-token workflow.

## What Was Intentionally Not Added

Milestone 6 does not add:

- React login UI
- Auth0 tenant setup automation
- user registration pages
- password storage
- refresh token handling
- account management screens
- ownership-based authorization checks for specific records
- claims transformation backed by the database
- domain events or transactional outbox
- event sourcing
- cloud deployment

Those are separate milestone decisions.

## What To Remember

Prefer standards-based OIDC/OAuth/JWT over custom token systems.

Keep provider-specific identity details at the API edge.

Keep Application code dependent on `ICurrentUser`, roles, and policies rather than Auth0, JWT libraries, or ASP.NET Core HTTP details.

Use policies for endpoint permissions because they age better than scattered role strings.

Treat uncertainty as unauthenticated or unauthorized. If the app is not sure a user is authenticated, return `false` and let the security gate block the request.

Keep protected endpoint response metadata in gate order for future controllers.
