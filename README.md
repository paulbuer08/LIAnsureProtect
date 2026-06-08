# LIAnsureProtect

LIAnsureProtect is a production-style cyber specialty insurance platform built for learning and portfolio depth. It is inspired by specialty insurance workflows, but it is not affiliated with or copied from any insurer.

The first product scope is a Cyber MVP. The system will support customer and broker submissions, insured company profiles, cyber questionnaires, document handling, risk scoring, underwriting review, quotes, policies, claims, notifications, observability, and later AI-assisted document review.

## Target Stack

- Backend: ASP.NET Core Web API with C# and .NET 10
- Architecture: practical Clean Architecture
- Application patterns: practical CQRS with MediatR and FluentValidation planned for Milestone 4
- Database: PostgreSQL with Entity Framework Core
- Frontend: React 19, TypeScript, Vite
- Local platform: Docker Compose
- Cloud target: AWS
- AWS services over time: ECS Fargate, ALB, Lambda, API Gateway, RDS PostgreSQL, RDS Proxy, S3, SQS, SNS, DynamoDB, ElastiCache Redis, CloudWatch, WAF, Secrets Manager, Parameter Store, Terraform

## Build Style

This project is built milestone by milestone. Each milestone should be small enough to understand, test, document, and debug before moving on.

Before implementation, document the design. After implementation, update docs and the changelog.

## Current Status

Milestone 3 is dependency registration and architecture guards. The backend now has shared Application and Infrastructure dependency-registration extension methods, API and Worker startup use those shared methods, and tests protect the current project-reference boundaries.

Milestone 4 is complete as `Milestone 4 - Application Use Case Foundation`. It introduced practical CQRS, MediatR, FluentValidation, a validation pipeline behavior, and the first submission intake slice exposed through `POST /api/v1/submissions`.

The first slice uses an `ISubmissionRepository` interface in the Application layer and a temporary in-memory Infrastructure implementation so the API can still start before PostgreSQL is introduced. Unit of Work is intentionally deferred until the persistence milestone because it belongs with EF Core transactions and database `SaveChanges` behavior.

## Documentation

- [Project Status](docs/project-status.md)
- [Architecture Overview](docs/architecture/overview.md)
- [Cyber Specialty Insurance Overview](docs/business/cyber-specialty-insurance-overview.md)
- [User Roles](docs/business/user-roles.md)
- [Local Development](docs/dev/local-development.md)
- [Milestone Documentation Practice](docs/dev/milestone-documentation-practice.md)
- [Milestone 2 Backend Foundation Learnings](docs/dev/milestone-2-backend-foundation-learnings.md)
- [Milestone 3 Dependency Registration And Architecture Guards Learnings](docs/dev/milestone-3-dependency-registration-and-architecture-guards-learnings.md)
- [Milestone 4 Application Use Case Foundation Learnings](docs/dev/milestone-4-application-use-case-foundation-learnings.md)
- [ADR-005: Application Use Case Patterns](docs/architecture/decision-records/ADR-005-application-use-case-patterns.md)
- [AWS Environments](docs/dev/aws-environments.md)
