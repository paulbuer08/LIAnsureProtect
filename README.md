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

**Phase 1 is complete and hardened** (July 2026): the platform covers the full pre-bind cyber
specialty lifecycle — submission intake, rating, referral underwriting with evidence and advisory
AI, quote acceptance, and policy binding — restructured into a modular monolith (Platform kernel +
Notifications/Underwriting/Quoting modules, Strangler-Fig-carved from the legacy core) with
AWS-shaped adapters (S3, SNS/SQS, Redis) proven against LocalStack/Docker at zero cloud cost, a
permanent zero-warning analyzer gate, and 270+ tests. **Next: Phase 2 — Terraform + real AWS.**

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
| [**The Build History**](docs/build-history.md) | How it got here: all milestones across six eras, with the why behind each step. |
| [**Running The App**](docs/guides/running-the-app.md) | Complete run manual: prerequisites, one-time Auth0 setup, the everyday three-terminal run, opt-in LocalStack/Redis. |
| [**Manual Testing Guide**](docs/guides/manual-testing-guide.md) | Walk the UI as every role with generic test personas and end-to-end scenarios. |
| [**Project Status**](docs/project-status.md) + [**Roadmap**](docs/dev/production-transformation-roadmap.md) | Where we are; the fully-baked plan for what comes next. |

Everything else — per-milestone design/learnings records, ADRs, concepts, conventions — is indexed
by the [Documentation Map](docs/README.md), with the per-milestone archive under `docs/dev/`.