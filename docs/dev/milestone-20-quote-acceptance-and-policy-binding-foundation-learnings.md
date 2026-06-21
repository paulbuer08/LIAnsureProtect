# Milestone 20 - Quote Acceptance And Policy Binding Foundation Learnings

Status: started.

Branch:

```text
codex/milestone-20-quote-acceptance-and-policy-binding-foundation
```

Starting point:

```text
811f459 docs: close external rating provider resilience milestone
```

## Plain-English Goal

Milestone 20 should add the first safe path from quote to policy.

In insurance terms:

- quote creation says, "Here is the price and proposed coverage the system can offer or refer";
- quote acceptance says, "The customer or broker wants to proceed with this quote";
- policy binding says, "Coverage is now in force according to the recorded terms."

That is a bigger business step than rating. A quote can be exploratory. A bound policy is a durable contract state, so it needs stronger rules, clearer audit, and careful idempotency.

## Starting Assumptions

- Milestone 17 created local cyber quotes.
- Milestone 18 added underwriting referral and review actions.
- Milestone 19 added external provider market indications but kept the local quote authoritative.
- Milestone 20 should build on those foundations instead of replacing them.

## Recommended Scope To Finalize In The Milestone 20 Thread

- Add a quote acceptance use case for eligible quotes.
- Add a policy-binding use case that creates a durable policy from an accepted quote.
- Add a PostgreSQL policy table.
- Add policy number generation.
- Add effective date and expiration date fields.
- Add policy status fields, likely starting with a small lifecycle such as `Bound`.
- Add authorization rules for who can accept a quote and who can bind a policy.
- Preserve underwriter controls for referred quotes.
- Preserve idempotency on high-risk POST actions.
- Capture a policy-bound domain event into the existing transactional outbox.
- Add focused backend tests and a migration guard.

## Recommended Out Of Scope

- Real payment collection.
- Production policy documents.
- Carrier binding APIs.
- Real e-signature workflows.
- Endorsements.
- Cancellation.
- Renewal.
- Reinstatement.
- Claims.
- Billing and collections.
- Notification publishing.
- Notification inboxes.
- Advisory AI.

## First Planning Questions

The Milestone 20 implementation thread should decide:

- Whether quote acceptance and policy binding are one endpoint or two separate use cases.
- Whether customer/broker acceptance alone can bind, or whether an underwriter/admin binding action is required.
- What policy number format is acceptable for the learning slice.
- Which quote statuses are eligible for acceptance.
- How adjusted underwriting decisions affect final policy terms.
- Which fields belong on the policy aggregate now versus later.

## Verification Target

Expected verification path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```
