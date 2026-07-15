# Quote supersession and reassessment governance — implementation learnings

This record explains how the approved design in
`quote-supersession-and-reassessment-governance-design.md` became working behavior.

## Outcome

One Submission may retain many Quote versions, but only one pre-contract version is current. Creating
version N+1 timestamps and supersedes N without deleting it. Underwriting evidence and Notification
inboxes keep their own workflow/read state while also recording whether their Quote is current or
historical.

This separation matters. `Unread` answers *did this user open the event?* while `Historical` answers
*can this event still require work?* Automatically marking an old event read would rewrite human history;
leaving it active would inflate badges and operational queues. The system therefore keeps the original
read timestamp and excludes historical rows from active counts.

## Implementation map

- Quoting owns `Quote.SupersededAtUtc`, `ReassessmentRequest`, base-version concurrency, allowances,
  pending review, and immutable Quote history.
- Underwriting owns `QuoteEvidenceDisposition`. The Quote-assurance projector historicalizes lower
  versions before creating requests for the real new version. Domain guards reject every historical
  mutation.
- Notifications owns `NotificationLifecycleState`. Its projector historicalizes lower Quote versions;
  unread queries count only active rows.
- React exposes nested Quote history, current/historical Evidence filters, read-only historical detail,
  muted historical notifications, queued-customer feedback, and an Underwriter approval queue.
- SignalR still sends only a payload-free refresh hint after a committed projection. Authorized HTTP
  reads remain the source of truth.

## Reassessment safeguards

The durable policy starts with two successful self-service reassessments per rolling 24 hours, five over
the pre-contract lifetime, a post-success 30-minute cooldown, one pending manual review, and three
attempts per ten minutes per user and Submission. The original version-1 Quote is the baseline, not a
reassessment, so it never starts the cooldown: the first valid reassessment is immediate. A request
during a cooldown is rejected with retry guidance and creates no Underwriter work. Only a request beyond
the rolling or lifetime count allowance persists its normalized snapshot for Underwriter approval. It
does not call the rating provider, create a Quote, or generate Evidence until approved.

Approval rechecks that the base Quote is still current. A stale request becomes `Stale`; it cannot
supersede a newer Quote. Approval and the new Quote save in one Submission-context transaction. Decline
records actor, time, and reason while leaving the current Quote unchanged.

## Database changes and upgrade order

Apply the normal context order used by `scripts/update-database.ps1`:

1. `SubmissionDbContext`: `AddQuoteSupersessionAndReassessmentGovernance`
2. `UnderwritingDbContext`: `AddQuoteEvidenceDisposition`
3. `NotificationsDbContext`: `AddNotificationLifecycle`
4. `ClaimsDbContext`: unchanged, but still checked by the migration gate

The first migration backfills timestamps for already-superseded Quotes. The next two use Quote identity
only in one-time upgrade SQL to label legacy projections. Runtime code remains context-local and uses the
transactional outbox/projectors; no module performs a cross-context write.

## Important failure boundaries

- Unchanged or contradictory controls fail before governance or provider work.
- A stale `BaseQuoteVersion` fails before provider work.
- A post-reassessment cooldown rejects the request without creating a pending review; rolling/lifetime
  allowance overflow creates at most one pending request.
- Historical Evidence rejects responses, files, reminders, reviews, and acknowledgements in the Domain,
  even if a caller bypasses the browser.
- A lower-version Notification becoming historical does not acquire a fake `ReadAtUtc`.
- Historical rows remain owner/policy scoped; history never weakens authorization.

## Verification focus

Regression coverage proves queued-then-approved reassessment, Quote version/supersession linkage,
historical Evidence projection, separate Notification lifecycle/read state, and active-only unread counts.
The standard closeout also runs a zero-warning solution build, full backend tests, all four pending-model
checks, frontend type/lint/test/build gates, and fresh-Docker local CI.

## Post-merge self-service cooldown correction

The first implementation used the latest Quote timestamp for cooldown regardless of version. That made
the original Quote behave as though it were already a successful reassessment, so a customer's first
valid change could be queued for Underwriter approval immediately after Quote version 1 was generated.
An integration test had accidentally encoded that result instead of the product rule.

The corrected boundary separates two different safeguards:

- **time throttle:** after version 2 or later is created, a new attempt inside 30 minutes is rejected
  with safe retry guidance and no provider call, Quote, Evidence, Notification, or pending review;
- **cost allowance:** after two successful reassessments in the rolling day, or five over the
  pre-contract lifetime, a valid changed request is queued for an audited Underwriter decision.

Regression coverage now proves the first reassessment creates version 2 immediately, the next attempt
inside the post-success cooldown is rejected without a pending row or provider attempt, and count
overflow still queues one request that approval can turn into the next immutable Quote version.

Pending requests created before this correction are retained. Their persisted record does not identify
whether the old implementation queued them because of time or count, so silently approving, declining,
or deleting them would invent audit history. A local test account can resolve such a row through the
existing Underwriter decision or reset its local development data.

### Correction verification evidence

The 2026-07-16 correction passed every required gate:

- the whole solution built with `0 Warning(s)` and `0 Error(s)`;
- standalone backend tests passed with 226 Unit tests and 284 Integration tests, plus four intentional
  environment-gated service tests skipped;
- `SubmissionDbContext`, `NotificationsDbContext`, `UnderwritingDbContext`, and `ClaimsDbContext`
  each reported no pending model changes;
- frontend TypeScript, ESLint, production build, and all 111 Vitest tests passed; and
- full Docker-backed local CI recreated PostgreSQL and Redis, applied all four migration histories,
  passed 226 Unit tests and 285 Integration tests plus three intentional service skips, clean-installed
  279 frontend packages with zero vulnerabilities, passed frontend and API smoke gates, produced
  `TestResults/local-ci-20260716-055648.zip`, and removed its containers, database volume, and network.

## Final verification evidence

The 2026-07-16 closeout passed every required gate:

- the whole solution built with `0 Warning(s)` and `0 Error(s)`;
- backend tests passed with 219 Unit tests and 283 Integration tests, plus three intentionally
  environment-gated external-service tests skipped by the Docker-backed default run;
- `SubmissionDbContext`, `NotificationsDbContext`, `UnderwritingDbContext`, and `ClaimsDbContext`
  each reported no pending model changes;
- a clean `npm ci` reported zero vulnerabilities, the production frontend build and ESLint passed,
  and all 111 Vitest tests passed; and
- full local CI recreated PostgreSQL and Redis, applied all four migration histories, exercised the API
  smoke checks, produced `TestResults/local-ci-20260716-031625.zip`, and removed its containers,
  database volume, and network afterward.

Vite/Rolldown still prints the known non-failing `INVALID_ANNOTATION` advisory for two misplaced
`/*#__PURE__*/` comments inside the published `@microsoft/signalr` dependency. The build exits zero and
the warning does not originate in application code; no dependency file is patched in the repository.
