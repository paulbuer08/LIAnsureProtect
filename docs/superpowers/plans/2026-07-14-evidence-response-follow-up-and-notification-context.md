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

## Phase C — realtime notification invalidation and complete deep links

1. Add an authenticated, server-to-client-only `NotificationHub` with a payload-free
   `NotificationsChanged` contract.
2. Publish only after the Notifications inbox projection commits. Redis/SignalR failure is advisory,
   logged, and cannot fail the durable outbox message.
3. Connect the React shell with automatic reconnect; invalidate inbox/count queries on connect,
   hint, and reconnect while keeping focus/navigation refresh as a safety net.
4. Add exact personal/team Claim notification destinations and URL-selected Claims adjudication.
5. Use the Redis backplane across the separate API and Worker processes now; reuse it for future
   multi-instance scale-out.

## Phase D — connection-pool governance

1. Register one shared `NpgsqlDataSource` per host and route every current DbContext through it.
2. Configure and validate separate API/Worker maximums, timeouts, idle pruning, and connection lifetime.
3. Document the replica budget equation, Npgsql metrics, PostgreSQL inspection, and the measured
   RDS Proxy/PgBouncer decision gate. Do not introduce sharding or denormalization without evidence.

## Phase E — verification and closeout

1. Add domain, handler, endpoint, mapper, and frontend tests without weakening existing assertions.
2. Update Tier-1 docs, the manual test guide, encyclopedia flows, build history, and changelog.
3. Require zero-warning build, full backend tests, all four pending-model checks, frontend
   TypeScript/lint/tests/build, and Docker-backed local CI.
4. Deliver through the protected-main PR flow, inspect all review threads, squash-merge only when
   green, resynchronize `main`, and delete only safely stale branches.
