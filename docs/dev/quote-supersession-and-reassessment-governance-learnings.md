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
the pre-contract lifetime, a 30-minute cooldown, one pending manual review, and three attempts per ten
minutes per user and Submission. A request outside the immediate allowance persists only its normalized
snapshot. It does not call the rating provider, create a Quote, or generate Evidence until an Underwriter
approves it.

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
- A cooldown/allowance overflow creates at most one pending request.
- Historical Evidence rejects responses, files, reminders, reviews, and acknowledgements in the Domain,
  even if a caller bypasses the browser.
- A lower-version Notification becoming historical does not acquire a fake `ReadAtUtc`.
- Historical rows remain owner/policy scoped; history never weakens authorization.

## Verification focus

Regression coverage proves queued-then-approved reassessment, Quote version/supersession linkage,
historical Evidence projection, separate Notification lifecycle/read state, and active-only unread counts.
The standard closeout also runs a zero-warning solution build, full backend tests, all four pending-model
checks, frontend type/lint/test/build gates, and fresh-Docker local CI.

