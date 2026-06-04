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
| ClaimsAdjuster | Reviews claims, requests documents, approves, denies, and closes claims. |
| Admin | Manages users, roles, products, rules, and system settings. |

## Ownership Rules

- Customers can access their own company, submissions, policies, documents, and claims.
- Brokers can access only assigned client companies and related records.
- Underwriters can access submissions assigned for underwriting review.
- Claims adjusters can access claims assigned for claims review.
- Admins can manage system-wide configuration.

## Implementation Direction

Use ASP.NET Core Identity for user accounts.

Use roles for broad categories and policies for business-specific ownership rules.

Example:

```text
Role check: Is this user an Underwriter?
Policy check: Is this underwriter assigned to this submission?
```
