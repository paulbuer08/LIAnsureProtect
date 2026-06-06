# LIAnsureProtect

LIAnsureProtect is a production-style cyber specialty insurance platform built for learning and portfolio depth. It is inspired by specialty insurance workflows, but it is not affiliated with or copied from any insurer.

The first product scope is a Cyber MVP. The system will support customer and broker submissions, insured company profiles, cyber questionnaires, document handling, risk scoring, underwriting review, quotes, policies, claims, notifications, observability, and later AI-assisted document review.

## Target Stack

- Backend: ASP.NET Core Web API with C# and .NET 10
- Architecture: practical Clean Architecture
- Database: PostgreSQL with Entity Framework Core
- Frontend: React 19, TypeScript, Vite
- Local platform: Docker Compose
- Cloud target: AWS
- AWS services over time: ECS Fargate, ALB, Lambda, API Gateway, RDS PostgreSQL, RDS Proxy, S3, SQS, SNS, DynamoDB, ElastiCache Redis, CloudWatch, WAF, Secrets Manager, Parameter Store, Terraform

## Build Style

This project is built milestone by milestone. Each milestone should be small enough to understand, test, document, and debug before moving on.

Before implementation, document the design. After implementation, update docs and the changelog.

## Current Status

Milestone 2 is the backend foundation. The solution, backend project structure, API host, Worker placeholder, and root/health endpoint integration tests are in place.

## Documentation

- [Project Status](docs/project-status.md)
- [Architecture Overview](docs/architecture/overview.md)
- [Cyber Specialty Insurance Overview](docs/business/cyber-specialty-insurance-overview.md)
- [User Roles](docs/business/user-roles.md)
- [Local Development](docs/dev/local-development.md)
- [Milestone Documentation Practice](docs/dev/milestone-documentation-practice.md)
- [Milestone 2 Backend Foundation Learnings](docs/dev/milestone-2-backend-foundation-learnings.md)
- [AWS Environments](docs/dev/aws-environments.md)
