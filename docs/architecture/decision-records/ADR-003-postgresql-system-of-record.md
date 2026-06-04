# ADR-003: Use PostgreSQL As The System Of Record

## Status

Accepted

## Context

Insurance workflows are relational. Customers, brokers, insured companies, submissions, documents, quotes, policies, claims, audit logs, and user assignments need consistent relationships and clear history.

## Decision

Use PostgreSQL as the primary system of record with Entity Framework Core.

Use Redis for cacheable lookup data and short-lived performance helpers.

Use DynamoDB later for notification inbox/read-model workloads, not as the main business database.

## Consequences

PostgreSQL gives strong relational modeling, transactions, constraints, and reporting options.

Redis and DynamoDB are useful, but they should support the relational core instead of replacing it.
