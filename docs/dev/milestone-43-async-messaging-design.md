# Milestone 43 — Real Async Messaging (Design)

## One-sentence goal

Make the notification **publish** step hit a **real message bus** — Amazon SNS, fanning out to an
SQS queue with a dead-letter queue (DLQ) — under `Platform:Profile=Aws`, filling the
`INotificationPublisher` "AWS SNS/SES arrives in a later milestone" seam, **developed and tested
against LocalStack (no AWS account, no bill)** and **without changing any business flow, endpoint,
EF model, schema, the outbox, or the frontend**.

## Why this milestone exists (simple English)

Today, when the background Worker drains the outbox, the `NotificationOutboxMessageConsumer`
projects the notification into the inbox (in-process) and then "publishes" it through
`LocalNotificationPublisher` — which just returns a fake id. Nothing actually leaves the process.

For a real system, published events must land on a **durable, networked bus** so other things can
react asynchronously: other bounded contexts, a future extracted microservice, or an analytics
sink — without the publisher knowing or waiting for them.

**SNS** (Simple Notification Service) is AWS's publish/subscribe hub: you publish one message to a
**topic**, and SNS fans it out to every **subscriber**. **SQS** (Simple Queue Service) is a durable
queue; subscribing an SQS queue to an SNS topic means every published message is safely stored in
the queue until something processes it. A **DLQ** is a second queue that catches messages a
consumer repeatedly fails to process, so one poison message never blocks the queue.

> **Analogy:** today the mailroom writes "sent!" in a logbook but never actually mails anything.
> M43 connects a real outbound mail chute (SNS). Drop one letter in the chute and the post office
> copies it into every subscriber's mailbox (SQS queues). A letter that can't be delivered after
> several tries goes to a "return to sender" bin (DLQ) instead of jamming the chute.

## The one design rule that makes this safe

We **do not** change the outbox, the dispatcher, the retry/poison machinery, or the in-process
projection. We only replace **one adapter** behind the existing `INotificationPublisher` port:

- `Local` profile → `LocalNotificationPublisher` (unchanged).
- `Aws` profile → **new `SnsNotificationPublisher`**.

This works because the pieces already fit:

- The outbox row already stores a `ProviderMessageId` — it now holds the **real SNS message id**.
- The dispatcher already treats a publish failure as a retry (`TransientFailure`) or a poison
  (`PermanentFailure`) — a transient SNS error reuses that path untouched.
- In-process projection (inbox, team inbox, referral operations) **stays in-process** because those
  need read-your-writes; only the outward *publish* becomes a real network call. This is exactly
  the split the roadmap called for.

## No AWS account needed: LocalStack (again)

The same LocalStack container M42 used for S3 also emulates **SNS and SQS**. The identical SNS SDK
code talks to LocalStack when we point the client's `ServiceURL` at `http://localhost:4566`; it
talks to real AWS by dropping `ServiceUrl` and setting a region + letting the default credential
chain (task/instance role) supply credentials — no static keys in the cloud. So M43 costs nothing
and the Phase-2 cutover is configuration only.

## What gets built

### 1. Packages
- `AWSSDK.SimpleNotificationService` (production, Notifications.Infrastructure).
- `AWSSDK.SQS` (test-only, IntegrationTests) — used solely to prove the SNS→SQS round trip.

### 2. `NotificationPublisherOptions`
A `Sns` sub-section: `TopicArn` (**required** under Aws), `ServiceUrl` (LocalStack), `Region`,
`AccessKeyId`/`SecretAccessKey` (LocalStack dummy creds only; empty in real AWS).

### 3. `SnsNotificationPublisher : INotificationPublisher`
- Serializes the `NotificationMessage` into a **versioned JSON envelope** (a stable integration
  contract: schema version, message id, outbox id, type, audience, owner, subject reference,
  occurred-at, attributes) — the analytics/event-spine contract.
- Publishes to the topic with **SNS message attributes** (`type`, `audience`) so subscribers can
  filter without parsing the body.
- Returns `Success(sns.MessageId)` (→ recorded on the outbox row), or `TransientFailure` on an
  `AmazonSNSException`/transient error (→ existing retry with backoff).

### 4. Composition-root wiring
`AddNotificationsModule`'s `Aws` branch (which used to `throw`) now registers
`IAmazonSimpleNotificationService` (LocalStack vs real AWS decided by `ServiceUrl`) and
`SnsNotificationPublisher`, **failing fast** if `TopicArn` is missing. The hosts bind
`NotificationPublisherOptions` from the `Notifications` config section.

## How this is tested (and why CI stays green)

1. **Adapter unit tests** (normal `dotnet test`, no Docker): a mocked
   `IAmazonSimpleNotificationService` proves *our* logic — publish targets the configured topic,
   the envelope + message attributes are built correctly, success maps the SNS message id, and a
   thrown `AmazonSNSException` maps to `TransientFailure`.
2. **New notification profile-switch test**: `Aws` now wires `SnsNotificationPublisher` (given
   config), and a missing topic **fails fast**.
3. **Opt-in LocalStack round-trip test** (env-gated exactly like the S3 and PostgreSQL opt-ins):
   creates an SNS topic + an SQS queue with a **redrive policy → DLQ**, subscribes the queue
   (raw message delivery), publishes through `SnsNotificationPublisher`, and asserts the message
   arrives in SQS. Skipped by default, so the required CI job stays green; the LocalStack compose
   service now enables `s3,sns,sqs`.

## Explicitly out of scope (later milestones)

- **An always-on SQS-consuming Worker loop** — there is no in-process consumer that needs the
  queue yet (projection stays in-process for read-your-writes). Building a background consumer that
  does nothing today would be speculative. The SQS+DLQ topology is created and proven; wiring a
  real downstream consumer arrives when a genuine consumer exists (an extracted service or the
  analytics sink).
- **Optional S3 event-archive sink** — a straightforward later add (subscribe a Firehose/S3 sink
  to the same topic); the versioned envelope is designed for it.
- **Publishing non-notification outbox events** as integration events — the same M40
  consumer/registry seam extends to this later; M43 covers the customer/team notification path.
- **Real topic/queue/IAM provisioning** — Terraform in Phase 2 (M45/M46).

## Acceptance criteria

- All existing tests pass unchanged (outbox/dispatcher/notification behavior is untouched).
- New adapter unit tests + notification switch test pass in normal CI.
- The opt-in LocalStack SNS→SQS round-trip test passes where LocalStack runs.
- `docs/encyclopedia/` Chapters 2, 3, and 10 updated to show SNS as the live notification bus.
- Learnings, CHANGELOG, project-status, and roadmap status updated.
