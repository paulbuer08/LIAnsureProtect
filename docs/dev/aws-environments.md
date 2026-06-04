# AWS Environments

LIAnsureProtect should keep development, staging, and production isolated.

The safest approach is separate AWS accounts:

- Dev account
- Staging account
- Production account

If separate accounts are not available at first, use separate Terraform state, VPCs, subnets, security groups, databases, buckets, queues, secrets, and IAM roles per environment.

## Environment Isolation

Each environment should have its own:

- VPC and subnets
- RDS PostgreSQL instance or cluster
- Redis/ElastiCache instance
- S3 document bucket
- DynamoDB tables
- SNS topics and SQS queues
- Secrets Manager secrets
- Parameter Store paths
- CloudWatch log groups and alarms

## Naming Convention

Use names that include the project and environment:

```text
liansureprotect-dev-documents
liansureprotect-staging-documents
liansureprotect-prod-documents
```

## Security Baseline

- Encrypt data at rest.
- Use private S3 buckets.
- Use least-privilege IAM.
- Keep secrets out of Git.
- Use WAF for public web entry points.
- Use backups and failover for production relational data.
- Use lifecycle rules for S3 objects.

## Deployment Direction

The first production container track is ECS Fargate behind an Application Load Balancer.

The second production track is Lambda behind API Gateway.

Both tracks should share the same application boundaries where practical, but deployment infrastructure should be documented separately.
