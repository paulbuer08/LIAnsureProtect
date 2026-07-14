# Evidence response follow-up and notification context — implementation plan

## Phase A — Evidence audit history

1. Add immutable Evidence response history and response-linked documents.
2. Add required respondent email, optional phone, and optional other concerns.
3. Permit append-only follow-up only for `Responded + NotReviewed` and preserve remediation behavior.
4. Expose owner and Underwriting detail reads with response history.
5. Correct legacy system-assurance Optional rows to Required.

## Phase B — Notification context and read behavior

1. Enrich quote/evidence event snapshots with company and Submission reference.
2. Group the inbox by Submission context and label every relevant card.
3. Mark actionable notifications read before navigation and remove standalone manual-read controls.
4. Add a role-aware unread-count API refreshed by meaningful navigation, focus, and cache events;
   do not continuously poll.

## Phase C — verification and closeout

1. Add domain, handler, endpoint, mapper, and frontend tests without weakening existing assertions.
2. Update Tier-1 docs, the manual test guide, encyclopedia flows, build history, and changelog.
3. Require zero-warning build, full backend tests, all four pending-model checks, frontend
   TypeScript/lint/tests/build, and Docker-backed local CI.
4. Deliver through the protected-main PR flow, inspect all review threads, squash-merge only when
   green, resynchronize `main`, and delete only safely stale branches.
