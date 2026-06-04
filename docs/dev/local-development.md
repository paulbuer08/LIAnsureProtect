# Local Development

This project should be runnable locally before AWS is introduced.

## Required Tools

- .NET SDK 10
- Docker Desktop
- Node.js and npm
- Git

Later milestones also need:

- AWS CLI
- Terraform

## Current Tool Notes

The current machine has .NET 10 and Docker available.

Node.js/npm, AWS CLI, and Terraform still need to be installed or fixed in PATH before frontend and cloud milestones.

## Local Services Planned

Docker Compose will eventually run:

- PostgreSQL
- Redis
- DynamoDB Local
- LocalStack
- MailHog or smtp4dev

The backend and frontend may run directly on the host during early development for easier debugging.

## Development Rule

Work milestone by milestone.

For each milestone:

1. Explain the design.
2. Create or update the smallest useful set of files.
3. Run the relevant verification command.
4. Update docs and changelog.
5. Commit only after the milestone is stable.
