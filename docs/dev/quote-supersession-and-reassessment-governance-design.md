# Quote supersession and reassessment governance — design

**Status:** approved for automatic implementation on
`feat/evidence-upload-and-notification-usability`.

## Problem

A reassessment correctly creates an immutable quote version, but the surrounding read models currently
behave as if every version remains current. Evidence requests can lose the quote version and default to
version 1, old requests remain actionable, old notifications remain active unread items, and the
submission page exposes only the latest quote link. Repeated reassessments can therefore create an
ambiguous and unnecessarily expensive set of active-looking work.

The fix must preserve the audit record while making exactly one pre-contract quote version current.
Deleting earlier versions is not acceptable: they explain what the customer asserted, what was priced,
what evidence was requested, and why a later version exists.

> **Analogy:** a superseded quote is like an earlier signed revision of a contract draft. It belongs in
> the file and remains readable, but nobody should continue completing tasks against it after a newer
> revision becomes authoritative.

## Core decisions

1. Quote versions are immutable audit history. Version N+1 supersedes N; it does not correct N in place.
2. `QuoteStatus.Superseded` is the commercial lifecycle result. The quote also records when it became
   superseded.
3. Evidence workflow status and quote disposition are separate:
   - workflow status remains `Open`, `Responded`, `Accepted`, or `Cancelled`;
   - quote disposition is `Current` or `Superseded`.
4. Notification read state and lifecycle are separate:
   - read state records whether a person opened the event;
   - lifecycle is `Active` or `Historical`.
5. Superseding an unread item does not falsify its audit record by marking it read. It becomes historical
   and is excluded from active unread counts.
6. Historical quote, evidence, response, document, scan, and review data remain owner/role scoped and
   readable through exact routes.
7. Historical evidence cannot receive responses, documents, reminders, reviews, acceptance, cancellation,
   or follow-up acknowledgements.
8. New evidence requests always persist the real quote version. Version 1 is never an implicit runtime
   fallback for a reassessment.
9. The submission detail keeps the latest quote prominent and links to a nested quote-history page. A
   top-level Quotes navigation item is unnecessary because quotes belong to one submission journey.
10. Cross-context lifecycle changes continue through the custom transactional outbox and idempotent
    projectors. No context writes another context's tables.

## Lifecycle projection

```mermaid
sequenceDiagram
    participant C as "Customer"
    participant Q as "Quoting transaction"
    participant O as "Transactional outbox"
    participant U as "Underwriting projector"
    participant N as "Notifications projector"
    C->>Q: "Create reassessment from latest version"
    Q->>Q: "Validate allowance, base version and changed controls"
    Q->>Q: "Supersede N and create N+1 atomically"
    Q->>O: "QuoteGenerated(N+1, supersedes N)"
    O->>U: "Project quote assurance"
    U->>U: "Mark N evidence historical; create N+1 requests"
    O->>N: "Project quote/evidence notifications"
    N->>N: "Mark lower-version entries historical"
    N-->>C: "Payload-free NotificationsChanged hint"
```

Projectors remain idempotent on the source outbox message id. Projection order is also self-healing:
processing either a new quote notification or a new evidence notification can historicalize lower quote
versions for the same submission.

## Customer behavior

- Active lists and overdue counts show current-version evidence by default.
- A `Historical` filter makes superseded requests discoverable.
- Historical request detail displays the original workflow state, quote version, superseded timestamp,
  and replacement quote link, but no mutation controls.
- Historical notifications use muted styling and actions named `View historical request` or
  `View historical quote`.
- Active unread badges count only `Active + unread` entries.
- The quote-history page is `/submissions/{submissionId}/quotes`; exact versions remain at
  `/submissions/{submissionId}/quotes/{quoteId}`.

## Underwriter behavior

- Superseded evidence leaves active queues, overdue counts, unreviewed counts, and pending-follow-up
  counts immediately after projection.
- The exact Underwriting evidence view remains readable with responses, documents, scan results, and
  reviews, but all decision/action controls are disabled.
- Historical notifications remain navigable but no longer contribute to an underwriter's active unread
  count.
- New quote-version requests are independent. Evidence is not silently carried forward because a newer
  assertion can require different proof.

## Reassessment resource governance

The browser form remains a local draft until the request succeeds. Server-side governance then applies
in layers:

1. **Idempotency:** repeated delivery of the same operation returns the first result.
2. **Base-version concurrency:** the request identifies the quote version it was based on. If another
   reassessment already won, the stale request is rejected without rating-provider work.
3. **One pending review:** one submission cannot accumulate multiple manual reassessment requests.
4. **Burst protection:** at most 3 reassessment attempts per customer and submission per 10 minutes;
   excess traffic receives `429` and `Retry-After`.
5. **Self-service allowance:** the starting policy permits 2 successful reassessments in a rolling
   24-hour window, a 30-minute cooldown, and 5 successful reassessments over the pre-contract lifetime.
6. **Manual overflow:** a valid changed request beyond the self-service allowance creates one lightweight
   `Pending` reassessment request. It does not call the rating provider or create evidence.
7. **Underwriter decision:** an authorized underwriter can approve or decline. Approval revalidates the
   base/current quote, then atomically creates N+1 and supersedes N. Decline leaves the current quote
   untouched and records reason, actor, and time.

Normal within-allowance reassessments remain immediate. Requiring approval for every reassessment would
add avoidable delay and operational load without improving the common happy path.

## Reassessment request audit model

The Quoting/legacy Submission context owns `ReassessmentRequest` with:

- submission, owner, base quote, and base version;
- normalized immutable request snapshot and fingerprint;
- `Pending`, `Approved`, `Declined`, or `Stale` status;
- requested, reviewed, and completed timestamps and actors;
- decision reason and the created quote id when approved.

The snapshot is an Application-owned serialized contract, not a foreign aggregate or cross-schema key.
Approval deserializes it through the same validator/rating workflow as immediate reassessment.

## Acceptance scenarios

1. Quote version 2 produces version-2 evidence requests and notifications.
2. Creating N+1 records `SupersededAtUtc` on N and preserves N's exact page.
3. N evidence keeps its original workflow status but becomes `Superseded` and rejects every mutation.
4. N evidence disappears from active customer and Underwriting queue/count results but is available through
   a historical filter and exact link.
5. N unread notifications become historical without acquiring a false read timestamp and no longer count
   in the badge.
6. New N+1 notifications and evidence are active and uniquely identify submission and quote version.
7. Quote history lists every owner-scoped version newest first and exact detail returns 404 for another
   owner.
8. A stale base version, unchanged controls, accepted/bound quote, cooldown, burst limit, or duplicate
   pending request performs no provider call and creates no quote/evidence work.
9. A within-allowance reassessment creates N+1 immediately.
10. Beyond the allowance, one pending request is created; underwriter approval creates N+1 only after
    revalidation, and decline creates no quote.
11. All projection retries remain idempotent and SignalR carries only a refresh hint.

## Deliberate deferrals

- automatic carry-forward of previously accepted evidence;
- post-bind endorsements and renewal reassessment;
- customer-paid reassessment allowances;
- distributed quota storage beyond the existing API limiter plus durable database policy;
- deletion or physical archival of historical quotes, evidence, or notifications.
