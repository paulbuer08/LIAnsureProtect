# LIAnsureProtect

LIAnsureProtect is a production-style cyber specialty insurance platform built for learning and portfolio depth. It is inspired by specialty insurance workflows, but it is not affiliated with or copied from any insurer.

The first product scope is a Cyber MVP. The system will support customer and broker submissions, insured company profiles, cyber questionnaires, document handling, risk scoring, underwriting review, quotes, policies, claims, notifications, observability, and later AI-assisted document review.

## Target Stack

- Backend: ASP.NET Core Web API with C# and .NET 10
- Architecture: practical Clean Architecture
- Application patterns: practical CQRS with MediatR and FluentValidation
- Database: PostgreSQL with Entity Framework Core and pgvector-ready local development
- Frontend: React 19, TypeScript, Vite
- Local platform: Docker Compose for application dependencies
- Cloud target: AWS
- AWS services over time: ECS Fargate, ALB, Lambda, API Gateway, RDS PostgreSQL, RDS Proxy, S3, SQS, SNS, DynamoDB, ElastiCache Redis, CloudWatch, WAF, Secrets Manager, Parameter Store, Terraform

## Build Style

This project is built milestone by milestone. Each milestone should be small enough to understand, test, document, and debug before moving on.

Before implementation, document the design. After implementation, update docs and the changelog.

## Current Status

**Phase 1 complete + Phase 3 Claims delivered** (July 2026): the platform covers the full
cyber specialty lifecycle **end to end** — submission intake, rating, referral underwriting with
evidence and advisory AI, quote acceptance, policy binding, **and now post-bind claims (FNOL →
adjudication → settlement)** — as a modular monolith (Platform kernel + Notifications / Underwriting
/ Quoting / **Claims** modules, Strangler-Fig-carved from the legacy core) with AWS-shaped adapters
(S3, SNS/SQS, Redis) proven against LocalStack/Docker at zero cloud cost, a permanent zero-warning
analyzer gate, and 500+ tests. **Next: Phase 2 — Terraform + real AWS.**

The Customer/Broker policy journey is also complete: customers and brokers can follow separate
Submission, Quote, and Policy states, open owner-scoped policy pages from notifications, file an
eligible claim from Policy detail, delete Drafts, and withdraw eligible Submitted applications
without erasing audit history.

The quote journey now distinguishes a customer's security-control assertion from an independently
verified fact. Customers attest to detailed control coverage, receive an immediate risk assessment,
and see when a quote is provisional. Required evidence is created through the transactional outbox,
reviewed by Underwriting, and must be satisfied before acceptance. Pre-acceptance improvements use
immutable quote reassessment versions; automated document findings remain advisory.

Quote reassessment history is now governed rather than merely accumulated: exactly one pre-contract
Quote version is current, earlier Quote/Evidence/Notification records remain immutable history, and
the first valid reassessment remains immediate. A post-success cooldown returns retry guidance without
creating Underwriter work; rolling/lifetime allowance overflow can queue a human decision without
calling the rating provider. See the [design](docs/dev/quote-supersession-and-reassessment-governance-design.md) and
[implementation learnings](docs/dev/quote-supersession-and-reassessment-governance-learnings.md).

The latest product-hardening branch makes that journey precise and supportable: customer pages
never print raw API JSON or internal exception text; reassessment can be cancelled; evidence requests
use paged summaries and exact detail routes; quote/evidence notifications identify and open their exact
historical subject; and Production/Aws hosts emit structured diagnostic signals. CloudWatch log groups,
alarms, and browser RUM remain Terraform-owned production infrastructure, documented in the
[operations runbook](docs/dev/production-observability-and-customer-errors-runbook.md).

The same branch now adds immutable human Submission references, contextual search on every applicable
owner/operations collection, role-specific filters, semantic breadcrumbs, friendly local-time display,
safe form cancellation, and explicit Evidence document requirements. Search always narrows an already
owner/team/role-authorized dataset; it is not an authorization shortcut or a global cross-context index.
See the [implementation learnings](docs/dev/role-aware-search-navigation-and-form-safety-learnings.md).

The current evidence follow-up slice treats an Evidence request as an auditable conversation rather
than one editable response. Until Underwriting starts its review, the owner may append another
response, concern, or document; every response and attachment stays linked in history. Automatic
material-control requests require documentary proof, while manual requests keep an explicit
Required/Optional/Narrative-only contract. Notifications group by company and Submission reference,
open the exact subject, mark actionable messages read on open, and refresh the unread badge without
loading the whole inbox or continuously polling. A payload-free SignalR hint crosses Redis only after
the Worker commits the inbox projection; PostgreSQL remains authoritative. API and Worker also expose
explicit shared Npgsql pool limits instead of hidden per-context defaults. See the
[design](docs/dev/evidence-response-follow-up-and-notification-context-design.md) and
[implementation learnings](docs/dev/evidence-response-follow-up-and-notification-context-learnings.md).

The current manual-test follow-up keeps Quote versions nested under their Submission but makes the exact
version reachable from Submission detail. Confirmation-dialog explanations now start behind an accessible
**More details** disclosure. Evidence contacts distinguish Philippine mobile and telephone numbers, enforce
server-side format and length rules, and allow contact-only corrections as append-only follow-ups. Owners may
have five currently unread follow-ups; opening an exact entry in the Underwriting workbench records an audited,
idempotent acknowledgement and restores one slot. See the
[follow-up governance learnings](docs/dev/evidence-follow-up-governance-and-quote-navigation-learnings.md).

- The story of every milestone: [**The Build History**](docs/build-history.md)
- The precise current state: [Project Status](docs/project-status.md) · [Changelog](CHANGELOG.md)
## Local Run

Run a fresh dependency stack, apply migrations, build, and start the API from the repository root:

```powershell
.\scripts\dev-up.ps1
```

That script resets the local Docker Compose dependency stack by default, removes the local PostgreSQL volume, starts PostgreSQL/pgvector, applies EF Core migrations through the repo-local `dotnet-ef` tool manifest, and runs the API.

For setup without tests or starting the API, run:

```powershell
.\scripts\setup-dev.ps1
```

To include tests in the setup run:

```powershell
.\scripts\setup-dev.ps1 -RunTests:$true
```

Run the combined local CI path, including backend setup/tests/smoke checks and frontend install/build/lint/test checks:

```powershell
.\scripts\run-local-ci.ps1
```

## Documentation

Start with the [**Documentation Map**](docs/README.md) — five documents answer 95% of questions:

| | |
|---|---|
| [**The Encyclopedia**](docs/encyclopedia/README.md) | How the system works today: technologies, architecture, design patterns, and every workflow in simple English with diagrams that mirror the code. Updated every milestone PR. |
| [**The Build History**](docs/build-history.md) | How it got here: all milestones across seven eras, with the why behind each step. |
| [**Running The App**](docs/guides/running-the-app.md) | Complete run manual: prerequisites, one-time Auth0 setup, the everyday three-terminal run, opt-in LocalStack/Redis. |
| [**Manual Testing Guide**](docs/guides/manual-testing-guide.md) | Walk the UI as every role with generic test personas and end-to-end scenarios. |
| [**Project Status**](docs/project-status.md) + [**Roadmap**](docs/dev/production-transformation-roadmap.md) | Where we are; the fully-baked plan for what comes next. |

Everything else — per-milestone design/learnings records, ADRs, concepts, conventions — is indexed
by the [Documentation Map](docs/README.md), with the per-milestone archive under `docs/dev/`.
