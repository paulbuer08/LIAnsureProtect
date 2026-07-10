# Customer/Broker Walkthrough Hardening and Policy Journey Plan

> **Status:** Parts 1-4 were delivered by the walkthrough-hardening branch. Part 5 was implemented on
> `feat/customer-broker-policy-journey` on 2026-07-11. Policy cancellation remains deliberately
> deferred: a bound Policy is never deleted, and cancellation will only be added with explicit
> effective-date/reason audit, Claims implications, notification events, and tests. See
> `customer-broker-policy-journey-learnings.md` for the implementation record.

## Why this work happened

The first real Customer walkthrough exposed a useful difference between an API that is technically
complete and a product journey that is understandable. The backend already had submission, quote,
policy, notification, evidence, and claims capabilities, but several customer actions were missing
from the UI, authentication recovery was too manual, and submission/quote/policy state was presented
as though it were one status.

The guiding rule is now:

> The API remains the final authorization and state-transition authority, while the frontend only
> renders actions and workspaces that the signed-in role can actually use.

## 1. Infrastructure and identity decisions preserved from the walkthrough

### Auth0 custom domain

- The Auth0 tenant's original `dev-...us.auth0.com` name is not renamed. Auth0 tenant names are
  effectively fixed; `auth.liansureprotect.com` is a custom issuer/domain placed in front of it.
- Auth0 supplied a CNAME target. Cloudflare temporarily hosts the CNAME as **DNS only** because
  Auth0 must terminate TLS and manage the custom-domain certificate itself. Cloudflare proxying is
  not needed and can interfere with Auth0 verification.
- Both sides of token validation must switch together:
  - frontend `VITE_AUTH0_DOMAIN=auth.liansureprotect.com`
  - API `Authentication__Authority=https://auth.liansureprotect.com/`
- The API and frontend local files currently use the custom domain. A mismatched issuer/authority
  causes token validation or role lookup failures even when login succeeds.
- When authoritative DNS moves to Route 53 in Phase 2, recreate the same Auth0 CNAME there before
  changing nameservers. DNS hosting changes; the Auth0 custom-domain target does not.

### Cloudflare and AWS have different jobs

- Cloudflare remains the registrar for `liansureprotect.com`.
- Cloudflare DNS is temporary. Phase 2 moves authoritative DNS to Route 53.
- The production edge remains AWS-native: Route 53 + CloudFront + AWS WAF + Shield + ACM.
- `DNS only` means Cloudflare answers the DNS lookup but does not proxy, cache, inspect, or protect
  the HTTP request. It does not mean "domain registration only"; DNS hosting and registration are
  separate services.
- Do not run Cloudflare CDN/WAF in front of CloudFront/WAF by default. That would duplicate caching,
  TLS, WAF, and troubleshooting layers without a demonstrated requirement.

### Local configuration convention

- The API now loads `src/LIAnsureProtect.Api/.env.local`, which is gitignored and has a committed
  `.env.example`. This is the preferred visible local configuration path for Auth0 settings.
- The frontend keeps the same convention under `src/LIAnsureProtect.Web/.env.local`.
- `VITE_API_BASE_URL=http://localhost:5223` is included in the frontend example and local file.
- ASP.NET Core User Secrets still work, but are no longer the primary walkthrough because they are
  harder to discover and inspect later.
- Never commit either `.env.local` file. The examples document names and safe local defaults without
  storing tenant-specific values or secrets.

### AWS services discussed for later milestones

- **EventBridge:** useful later for schedule-driven workflows, rules-based routing, SaaS/external
  integration, and broad event fan-out. It complements the transactional outbox; it does not replace
  atomic database event capture.
- **Lambda:** useful for S3-triggered document scanning, scheduled maintenance, small event consumers,
  and isolated automation. The main synchronous API does not need to move to Lambda merely to claim
  serverless usage.
- **Glue:** useful after operational data is exported to an analytics lake for portfolio, claims,
  underwriting, actuarial, and regulatory ETL/catalog workloads. It should not query or transform the
  transactional database in the customer request path.

### Transactional outbox decision

LIAnsureProtect has a custom transactional outbox. Domain state and an outbox message commit in one
database transaction; the Worker merge-orders pending messages and idempotent consumers project
notifications and other cross-context effects.

Ready-made alternatives exist if the project later chooses to trade custom control for framework
conventions: MassTransit transactional outbox, NServiceBus Outbox, CAP, Wolverine durable messaging,
and Debezium change-data capture. They are not drop-in replacements without migration work: message
schemas, retries, deduplication, ordering, observability, and module boundaries still need explicit
design. The current custom implementation is retained because it is already integrated and tested.

## 2. Implemented authentication, authorization, and navigation hardening

- The React app obtains server-authoritative roles from `GET /api/v1/me`.
- A shared role map drives the dashboard, top navigation, and route guards.
- Role-ineligible links and cards are omitted. Direct URL attempts are rejected before the protected
  page mounts or starts its API query.
- ASP.NET Core authorization policies remain the security boundary; hidden navigation is user
  experience, not security.
- The dashboard distinguishes:
  - roles loading;
  - role lookup unavailable because the API/token request failed;
  - authenticated user with no assigned product role.
- The Auth0 callback now returns to the dashboard automatically and reuses the cached API-audience
  token. This removed the repeated `Continue with Auth0` / `Continue to dashboard` loop.
- Local developer token/session panels were removed from the customer dashboard.
- The Worker now has a background `ICurrentUser` implementation so non-HTTP work does not fail while
  resolving user context.

## 3. Implemented Customer/Broker happy-path hardening

### Submission lifecycle

- Customer/Broker can create and list multiple owned draft submissions.
- Draft detail now supports edit/save/cancel before final submission.
- Only Draft submissions may be edited; ownership and state are enforced by the API.
- Draft detail exposes **Submit submission** and refreshes list/detail caches after success.
- Submission status intentionally remains `Submitted` after quoting and binding. A submission is the
  historical application; quote and policy are separate aggregates with separate lifecycles.

### Quote journey

- Submitted detail exposes quote generation.
- Customer/Broker can review and accept an eligible quote after naming the acceptor, title, and
  acknowledging subjectivities.
- An accepted quote can be bound from the same walkthrough.
- Submission detail now returns the latest quote, so quote state survives page refresh.
- Repeated quote-generation requests return the existing quote. They do not create duplicate quotes,
  outbox events, or "Your quote is ready" notifications.

### Rating intake

- Visible monetary values default to Philippine peso (`PHP`, symbol `₱`).
- Rating fields include concise help exposed on hover and keyboard focus. Help does not remain pinned
  after a click.
- Industry supports **Other** with a required description.
- Prior incident count above zero requires incident type and details. Severe incident types can drive
  underwriting referral because loss history is relevant to rating and human review.
- Customer and Broker share the same owner workflow and controls.

### Notifications

- The responsive top navigation shows unread notification count.
- Quote-related notifications link to the related submission.
- `Mark as read` means the notification itself was acknowledged; it is not evidence that a policy or
  quote document was opened.
- A bound-policy notification currently links to the related submission. This is a temporary product
  gap addressed by Part 5.

## 4. Domain decisions clarified during the walkthrough

### Submission, quote, and policy are not one status

The same business journey has three related records:

| Record | Meaning | Example states |
|---|---|---|
| Submission | The customer's application/intake snapshot | Draft, Submitted, Withdrawn |
| Quote | The insurer's offer and underwriting decision | Quoted, Referred, Approved, Declined, Accepted, Bound |
| Policy | The bound contract and coverage period | currently Bound; planned scheduled/active/expired/cancelled presentation |

Therefore a bound policy does not rewrite the historical submission to `Bound`. The UI must show
all three states clearly instead of presenting only `Submission status: Submitted` after binding.

### Multiple submissions are valid

Do not restrict a Customer to one lifetime submission or one active policy. A company may need
different products, entities, limits, renewal terms, or replacement applications. A Broker must also
act for multiple clients. The product should warn about likely accidental duplicates, but it should
not impose a universal one-submission rule.

### Records are retained, not cleaned away

- Submitted, quoted, accepted, bound, declined, and withdrawn business records are audit history.
- They must not be deleted as "debris" when the journey advances.
- Cleanup jobs apply to technical artifacts such as expired idempotency records, temporary files,
  retry metadata, and retention-governed operational data.
- Draft deletion may be allowed because no application was submitted yet. After submission, use an
  explicit state transition such as Withdrawn, Declined, Expired, Cancelled, or Closed.

### Review is conditional

- Clean risks can be quoted automatically.
- Referral triggers (risk controls, incident history, requested terms, or rating rules) send the quote
  to Underwriting review.
- Underwriting review is not a mandatory page in every clean customer journey.
- Evidence requests and human review remain available when the referral needs more information.

## 5. Approved next implementation: Customer/Broker policy journey

The next branch implements the following in order. Keep API authorization and owner scoping as the
final authority, preserve module boundaries, and add focused backend/frontend tests for each phase.

### Phase A - Role-correct notification experience

1. Read roles from the existing server-authoritative current-user query.
2. Customer/Broker with personal-only access see **no filter tabs**; they see one personal inbox.
3. Underwriter, ClaimsAdjuster, and Admin continue to see All / Personal / Team filters when their
   role grants team-notification access.
4. If the active filter becomes invalid after a role/session change, reset it safely.
5. Use subject type and attributes to choose an action label/route:
   - policy notification -> **View policy**;
   - quote/submission notification -> **Open submission**;
   - evidence notification -> **Open evidence request** where supported.

### Phase B - Owner-scoped policy read model and API

1. Add Customer/Broker/Admin policy list and detail queries behind a policy-read authorization rule.
2. Scope every read by `OwnerUserId`; return 404 for another owner's policy.
3. Return policy id/number, contractual status, effective/expiration dates, premium, limit, retention,
   related quote id, related submission id, and safe quote snapshot fields.
4. Model lifecycle language deliberately:
   - Bound/Scheduled before the effective instant;
   - Active while in force;
   - Expired after the expiration instant;
   - Cancelled only after a real cancellation command exists.
5. Do not fabricate an `Active` persisted state if the domain currently stores only `Bound`; either
   add an explicit tested transition model or expose a clearly named computed coverage state.

### Phase C - Policies frontend

1. Add `/policies` and `/policies/:policyId` routes for Customer/Broker/Admin.
2. Add **Policies** to role-aware navigation and dashboard only for eligible roles.
3. List policies independently from submissions, with status, policy number, coverage dates, premium,
   limit, retention, and client/company context available from safe owned data.
4. Detail page shows the policy first, then links to its source submission and quote history.
5. Show **File claim** only when the policy is eligible under the Claims context's existing rules.
6. Change policy-bound notification actions to `/policies/{policyId}`.

### Phase D - Make the combined journey understandable

1. On submission detail, render separate sections for Submission, Latest quote, and Related policy.
2. Add a computed journey stage for scanning (for example Draft -> Submitted -> Under review ->
   Quoted -> Accepted -> Policy active) without overwriting aggregate statuses.
3. Once a quote exists, do not render quote-generation controls again.
4. Once a policy exists, make **View policy** the primary next action; keep the source submission as
   read-only history.
5. Update notification copy so "Your quote is ready" leads to the actual quote section and "Your
   policy is bound" leads to the policy detail.

### Phase E - Withdrawal, cancellation, and duplicate controls

1. Allow owner deletion only for Draft submissions, with confirmation and authorization tests.
2. Expose the existing `Submission.Withdraw()` behavior through an idempotent owner command while the
   application has not produced an accepted/bound contract. Preserve the row and audit event.
3. Define quote withdrawal/decline/expiry behavior separately from submission withdrawal; do not
   overload one status to mean all three.
4. A bound policy is never deleted. Add policy cancellation only as an explicit audited domain
   workflow with effective date/reason and claims/notification implications.
5. Warn when a new draft appears to duplicate an existing open submission for the same owner/company,
   but allow the user to continue because multiple legitimate submissions are supported.
6. Keep all history required for insurance audit, customer support, and future renewal handling.

### Phase F - Documentation and verification

Update the Tier-1 docs as behavior lands: Encyclopedia Chapters 6, 9, and 10; Build History; Project
Status; Manual Testing Guide; Changelog. Verify using the repository's zero-warning gates, all context
migration checks, frontend type/lint/test/build, and full local CI before the implementation PR.

## Closeout verification for the walkthrough-hardening branch

- `dotnet build LIAnsureProtect.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test LIAnsureProtect.slnx --no-build`: UnitTests 189 passed; IntegrationTests 246 passed,
  4 opt-in service tests skipped.
- EF pending-model checks: no pending changes for `SubmissionDbContext`, `NotificationsDbContext`,
  `UnderwritingDbContext`, or `ClaimsDbContext`.
- Full Docker-backed local CI: passed with UnitTests 189, IntegrationTests 247 + 3 service skips,
  frontend TypeScript/Vite build, ESLint, and 77 Vitest tests.
- Artifact: `TestResults/local-ci-20260710-132954.zip`.

## Acceptance scenarios for the next branch

1. Customer with personal notifications sees no tabs and cannot access team entries by API or UI.
2. Underwriter/ClaimsAdjuster/Admin retains the appropriate team filters and audiences.
3. Bound-policy notification opens an owned policy detail, not a submission-only page.
4. Submission detail simultaneously shows `Submitted`, latest quote `Bound`, and policy coverage
   state without contradiction.
5. Customer/Broker sees all owned policies on `/policies` and cannot read another owner's policy.
6. Customer can create another legitimate submission even with an active policy.
7. Draft can be deleted; eligible submitted application can be withdrawn; bound policy cannot be
   deleted.
8. Refresh/retry does not duplicate quotes, policies, state transitions, or notifications.
