# Milestone 43 — Real Async Messaging (Learnings)

## What we set out to do

Make the notification **publish** step hit a real message bus (Amazon SNS → SQS + DLQ) under
`Platform:Profile=Aws`, filling the `INotificationPublisher` "SNS/SES arrives in a later milestone"
seam — **without changing the outbox, dispatcher, retry/poison machinery, in-process projection,
EF models, schemas, endpoints, or the frontend** — and **without an AWS account** (LocalStack).

## What shipped

- `AWSSDK.SimpleNotificationService` (v4) in Notifications.Infrastructure; `AWSSDK.SQS` (v4,
  test-only) in IntegrationTests.
- `NotificationPublisherOptions` with an `Sns` sub-section (`TopicArn`, `ServiceUrl`, `Region`,
  optional static creds).
- `SnsNotificationPublisher : INotificationPublisher` — publishes a **versioned JSON envelope**
  (`schemaVersion`, message id, outbox id, type, audience, owner, subject reference, occurred-at,
  attributes) to the topic with `type`/`audience` **SNS message attributes** for filter-policy
  routing; returns `Success(sns.MessageId)` or `TransientFailure` on `AmazonServiceException`.
- `AddNotificationsModule` `Aws` branch registers `IAmazonSimpleNotificationService` (LocalStack vs
  real AWS by `ServiceUrl`) + the SNS publisher, failing fast on a missing topic. Hosts bind
  `NotificationPublisherOptions` from the `Notifications` config section.
- Tests: 2 mocked-SNS adapter unit tests + 3 notification profile-switch tests (normal CI) and an
  opt-in LocalStack SNS→SQS+DLQ round-trip test.
- `docker-compose.yml` LocalStack now enables `s3,sns,sqs`; `.env.example` documents the switches.

## Decisions & why

### Reuse the `INotificationPublisher` port — don't invent a new abstraction
The module already had the exact seam: `case Aws: throw "SNS/SES arrives in a later milestone"`.
Filling it means the outbox row's existing `ProviderMessageId` now holds the **real SNS message
id**, and a transient SNS error reuses the dispatcher's existing retry/backoff/poison path. Least
code, least risk, best fit.

### Publish becomes networked; projection stays in-process
The in-process inbox/team/referral projections need read-your-writes (the API reads them
immediately after dispatch), so they stay in-process. Only the *outbound* publish goes to the bus.
This is the split the roadmap specified and avoids a distributed transaction across the hand-off.

### A versioned envelope, not the raw record
The published body is a `schemaVersion`-stamped envelope — the stable integration-event contract
that future subscribers and the analytics sink depend on. Bumping the version is how the contract
evolves without breaking consumers. `type`/`audience` are also SNS message attributes so
subscribers can filter without parsing the body.

### `AmazonServiceException` → transient
All SNS service/network errors (throttling, unavailable, timeouts) derive from
`AmazonServiceException`; catching that base and mapping to `TransientFailure` lets the dispatcher
retry and eventually park a poison message — no bespoke error taxonomy needed.

### Static keys only for LocalStack; fail fast on missing topic
Same stance as the M42 S3 adapter: static creds only when set (LocalStack), otherwise the default
credential chain in real AWS (no static keys in the cloud); resolving the publisher throws if
`Notifications:Sns` / `TopicArn` is missing under the Aws profile.

### Testing without "testing the mock"
Unit tests assert the request we build (topic, envelope, message attributes) and the result we map
(success → SNS id, exception → transient). The real SDK/bus behavior is proven by the opt-in
LocalStack round-trip: publish through the adapter and assert the message lands in a subscribed SQS
queue that has a DLQ redrive policy — skipped by default so the required CI job stays green.

## Gotchas

- **AWS SDK v4 consistency**: `AWSSDK.SimpleNotificationService` and `AWSSDK.SQS` both resolved to
  `4.0.100.1`, matching the M42 `AWSSDK.S3` version (shared `AWSSDK.Core` v4). Central package
  management pins all three so a future v5 is an explicit upgrade.
- **LocalStack services**: the compose `SERVICES` env expanded from `s3` to `s3,sns,sqs`; still
  profile-scoped (`aws-local`) so the default Postgres-only dev/CI flow is unaffected.
- **Raw message delivery**: the SQS subscription uses `RawMessageDelivery=true` so the queue body
  is our envelope JSON directly (no SNS notification wrapper), which the round-trip test parses.
- **CI stays green**: the round-trip test is env-gated (`LIANSUREPROTECT_RUN_SNS_TESTS`) and skipped
  by default; the required "Backend (build, migrate, test)" job does not run LocalStack.

## Verification

- `dotnet test`: UnitTests 66 passed; IntegrationTests 142 passed, 3 skipped (PostgreSQL, S3
  LocalStack, SNS LocalStack opt-ins).
- SNS→SQS round-trip run locally with `LIANSUREPROTECT_RUN_SNS_TESTS=true` against
  `docker compose --profile aws-local up -d` — message published to SNS arrived in the subscribed
  SQS queue with the DLQ redrive policy in place; envelope `type`/`ownerUserId`/`schemaVersion`
  verified.

## What M43 deliberately left for later

- **An always-on SQS-consuming Worker loop**: no in-process consumer needs the queue yet
  (projection stays in-process). Wiring a real downstream consumer arrives when a genuine consumer
  exists (an extracted service or the analytics sink).
- **Optional S3 event-archive sink**: subscribe an S3/Firehose sink to the same topic later; the
  versioned envelope is designed for it.
- **Publishing non-notification outbox events** as integration events via the same M40
  consumer/registry seam.
- **Real topic/queue/IAM provisioning**: Terraform in Phase 2 (M45/M46).
