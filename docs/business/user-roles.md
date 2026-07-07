# User Roles

LIAnsureProtect uses role-based and policy-based authorization.

Authentication answers: who is this user?

Authorization answers: what is this user allowed to do?

## Roles

| Role | Purpose |
| --- | --- |
| Customer | Creates their own company profile, submissions, documents, quote acceptance, and claims. |
| Broker | Manages assigned client companies and submits applications for clients. |
| Underwriter | Reviews submissions, requests more information, declines, and generates quotes. |
| ClaimsAdjuster | **Live (Phase 3).** Works the claims queue: assigns, requests information/documents, sets reserves, accepts/denies/settles, and closes claims. |
| Admin | Manages users, roles, products, rules, and system settings. |

> **Policy map (implemented):** `Submissions.*`, `Quotes.*`, `Policies.Bind`, `EvidenceRequests.Respond`
> (Customer/Broker/Admin); `Quotes.Underwrite` (Underwriter/Admin); `Notifications.Read`
> (Customer/Broker/Underwriter/**ClaimsAdjuster**/Admin); and the Claims policies —
> `Claims.File`/`Claims.Read`/`Claims.Respond` (Customer/Broker/Admin) and **`Claims.Adjudicate`**
> (ClaimsAdjuster/Admin). Roles are read **server-authoritatively** via `GET /api/v1/me` (the SPA
> never parses the token) and enforced by the API (403), so the UI and the server can't drift.

## Ownership Rules

- Customers can access their own company, submissions, policies, documents, and claims.
- Brokers can access only assigned client companies and related records.
- Underwriters can access submissions assigned for underwriting review.
- Claims adjusters can access claims assigned for claims review.
- Admins can manage system-wide configuration.

## Implementation Direction

Use standards-based OpenID Connect/OAuth with JWT access tokens for API authentication.

The API should validate tokens from an external identity provider such as Auth0, Amazon Cognito, Microsoft Entra External ID, or another compatible provider. Keep provider-specific details at the API edge so Application code depends on roles, policies, and the `ICurrentUser` abstraction instead of a specific vendor.

Do not build custom password or token logic in the API.

Use roles for broad categories and policies for business-specific ownership rules.

Example:

```text
Role check: Is this user an Underwriter?
Policy check: Is this underwriter assigned to this submission?
```

Current protected endpoint policy:

| Policy | Allowed roles | Purpose |
| --- | --- | --- |
| `Submissions.Create` | Customer, Broker, Admin | Allows creating draft submissions. |
| `System.Admin` | Admin | Reserved for system administration endpoints. |
