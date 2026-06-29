# Milestone 34 - Notifications Team Inbox — Learning Notes

## Goal

The first **feature built natively inside a carved module**. M33 moved the Notifications context into
its own module + `notifications` schema but kept behavior identical — only person-addressed
(`customer-or-broker`) notifications were persisted. M34 adds the deferred **team inbox**: persist the
`underwriting-operations` and `binding-operations` audiences the dispatcher was dropping, and let
internal staff read them with **per-user read receipts**.

## Confirmed product decisions

- `underwriting-operations` and `binding-operations` are both visible to **Underwriter + Admin** (binding
  is self-service today, so the binding desk is folded into the ops team for now).
- The UI gets **All / Personal / Team** filter tabs plus a "Team" badge — no new page, same endpoints.

## The shape: shared entry + per-user read receipts

A team notification is **shared** by an audience, so its read state can't live on the entry. Two tables
in the `notifications` schema:

- `team_notification_entries` — one row per source outbox message (unique on `source_outbox_message_id`,
  so projection stays idempotent), shared by everyone in the audience.
- `team_notification_read_receipts` — `(entry, user)` unique; created **lazily** the first time a user
  marks the notification read. No user directory is needed: team membership comes from the caller's
  role claim, and "unread" = "no receipt for me yet".

`TeamNotificationEntry.MarkReadBy(userId, readAt)` adds a receipt only if the user has none, so a
second member marking read never affects the first, and re-marks are idempotent.

## How read/mark-read merge personal + team

- `NotificationTeamAudiences.ForRoles(roles)` maps the caller's roles to the audiences they may read
  (Underwriter/Admin → both ops audiences; everyone else → none). It lives in the module and keeps its
  own role-name constants — a stable contract — so the module needn't reference the legacy
  `ApplicationRoles`.
- `ListMyNotificationsQueryHandler` merges personal entries (recipient == me) with team entries (audience
  ∈ my audiences), tagging each item with `Scope` (`"personal"`/`"team"`) + `Audience`, and sums the two
  unread counts.
- `MarkNotificationReadCommandHandler` tries the personal inbox first; if the id isn't the caller's
  personal notification, it tries the team inbox **limited to the caller's allowed audiences** — so a
  customer can never mark a team entry by guessing its id (returns 404).

The projector branches by audience: `customer-or-broker` → personal entry (existing); the two ops
audiences → team entry; anything else is skipped.

## The bug this milestone surfaced: `GetRoles()` vs `IsInRole()`

This is the first feature to read `ICurrentUser.GetRoles()` for filtering, and it exposed a latent
inconsistency in `HttpContextCurrentUser`:

- `IsInRole(role)` uses the **identity's** `RoleClaimType` (set by the JWT bearer to
  `Authentication:RoleClaimType` in production, and by the integration-test auth handler to `"roles"`).
- `GetRoles()` was re-reading the **config** `Authentication:RoleClaimType` directly.

In production both resolve to the same namespaced claim, so it worked. But the test auth handler emits
roles under `"roles"`, so `RequireRole` passed while `GetRoles()` returned empty — the underwriter's
team list came back empty. Fix: `GetRoles()` now reads the **identity's** `RoleClaimType`, so it always
agrees with `IsInRole()` in every environment. (Lesson: derive roles from one source — the identity —
not two.)

## Reuse, not reinvention

- The team projector reuses the **idempotent projection** pattern from M33's personal projector.
- The migration plumbing from M33 (two contexts, `--context`, scripts + CI applying both) needed **no
  change** — the new tables are just another `NotificationsDbContext` migration.
- The endpoints (`GET /api/v1/notifications`, `POST /api/v1/notifications/{id}/read`) are unchanged;
  team items merge into the same list and mark-read handles either.

## Authorization

`Notifications.Read` previously allowed Customer/Broker/Admin only — underwriters couldn't read any
notifications. Added `Underwriter` so they can reach their team inbox.

## Verification

- `dotnet build LIAnsureProtect.slnx` — 0/0.
- `dotnet test` — UnitTests 60, IntegrationTests 113 (+1 PostgreSQL opt-in skipped), incl. team
  projection idempotency, per-user read receipts (a second underwriter still sees the shared entry as
  unread), the audience guard, and the underwriter endpoint path.
- `dotnet ef migrations has-pending-model-changes` clean for both contexts.
- Frontend `vitest` green (tabs + Team badge); full local CI applies both contexts' migrations against
  fresh Docker Postgres.

## What's next

The Notifications module is now feature-complete for this phase. The next roadmap step is **M35 —
Underwriting module carve** (lift the Underwriting context out of the legacy `Quotes` namespace into
its own module + schema), continuing the strangler one context at a time.
