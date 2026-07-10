# Manual UI Testing Guide — Walk The App As Every Role

> **Living document**, updated in every milestone PR. All names, emails, and companies below are
> **generic test values** — never use real personal information in a test tenant.
>
> Prerequisite: the app is running and Auth0 is configured — follow
> [Running The App](running-the-app.md) first.

## The test personas (create these once in Auth0)

*User Management → Users → Create User*, then assign the role on each user's **Roles** tab.
Use one shared throwaway password pattern for your tenant (e.g. a password manager entry).

| Persona | Email (generic) | Auth0 role | Plays the part of |
|---|---|---|---|
| Casey Customer | `customer.test@example.com` | `Customer` | An insured company's contact buying cyber cover |
| Blake Broker | `broker.test@example.com` | `Broker` | A broker submitting on behalf of clients |
| Uma Underwriter | `underwriter.test@example.com` | `Underwriter` | The human decision-maker on referred quotes |
| Adrian Admin | `admin.test@example.com` | `Admin` | Superuser — allowed everywhere a business role is |
| Charlie Adjuster | `claims.test@example.com` | `ClaimsAdjuster` | **Live (Phase 3).** Works the claims workbench: assigns claims, requests info/documents, sets reserves, decides (accept/deny/settle), closes — Scenario 5 below |

> **Why one user per role?** Each browser session holds one login. Testing hand-offs
> (customer submits → underwriter reviews → customer responds) is easiest with two browser
> profiles or one normal + one private window, logged in as different personas.

## The map — what exists in the UI today

| Route | Who can use it | What it does |
|---|---|---|
| `/dashboard` | any signed-in user | Role-aware landing page; only shows the feature cards and menu links the signed-in role can use |
| `/submissions/new` | Customer, Broker, Admin | Create a draft submission |
| `/submissions` | Customer, Broker, Admin | List **your own** submissions (ownership-scoped) |
| `/submissions/:id` | owner | Detail: submit the draft, generate a quote, accept, bind |
| `/underwriting/quote-referrals` | Underwriter, Admin | The underwriting workbench: referral queue, SLA/triage, notes/tasks/timeline, evidence requests, AI review, approve/decline/adjust |
| `/evidence-requests` | Customer, Broker, Admin | Owner-side evidence: see requests, respond with text + up to 5 documents, upload replacements |
| `/notifications` | Customer, Broker, Underwriter, **ClaimsAdjuster**, Admin | Role-scoped inbox with unread counts and mark-read; the header shows a compact unread badge. Current walkthrough build still renders All/Personal/Team filters for every notification role; the next policy-journey slice removes all filters for personal-only Customer/Broker users. |
| `/claims/new` | Customer, Broker, Admin | File a claim (two-step wizard: pick a **bound policy** → incident form) |
| `/claims` · `/claims/:id` | owner | Your claims list + detail (verdict, claimed-amount, adjuster questions, scan-gated documents, timeline) |
| `/claims/adjudication` | **ClaimsAdjuster**, Admin | The adjuster workbench: queue, assign/release, reserves, information requests, accept/deny/close, documents, audit |

There is deliberately **no** admin console yet — `Admin` today means "allowed into every existing
business screen". The **Claims** context is live (Phase 3); `ClaimsAdjuster` now has a full workbench.

The dashboard and top navigation are intentionally role-aware. A customer should not see
underwriting or claims-adjudication links at all; an underwriter should not see customer-only
submission or claims filing actions. Direct URLs are still protected: the React route guard checks
the API-reported roles before mounting the page component, and the API authorization policy remains
the real enforcement point.

Three related statuses are deliberately separate. A Submission can remain `Submitted` after its
Quote becomes `Bound` and a Policy is created; that does not mean binding failed. The current detail
page does not present that separation clearly enough after refresh. The approved next slice adds
dedicated policy pages and shows Submission, Quote, and Policy state side by side.

## Scenario 1 — The happy path (clean quote, no referral)

**Persona: Casey Customer** (or Blake Broker — same flow, proving brokers act for clients).

1. Log in → **Dashboard** → *Create submission* (`/submissions/new`).
2. Fill with generic values and create:
   - Applicant name: `Jane Applicant`
   - Applicant email: `jane.applicant@example.com`
   - Company name: `Example Widgets Ltd`
3. ✅ Expect: success panel with a **Submission ID** and status **Draft**. Inline validation
   (clear the email field and try) proves Zod + React Hook Form gate bad input **before** any API call.
4. Open `/submissions` → ✅ your submission is listed (and *only* yours — ownership scoping).
5. Open the detail page → **Submit** the submission → ✅ status becomes **Submitted**.
6. **Generate quote** → the rating strategies run. A low-risk answer set produces status
   **Quoted** with premium/limit/retention and subjectivities.
7. **Accept** the quote: enter acceptor name/title (e.g. `Jane Applicant`, `CFO`), tick the
   subjectivities-acknowledged box → ✅ status **Accepted**. (Try accepting *without* the tick —
   expect a clear rejection: attestation is mandatory.)
8. **Bind policy** → ✅ a policy appears with a policy number and bound timestamps.
9. Open `/notifications` → ✅ quote-ready / accepted / policy-bound notifications with unread
   badges; mark one read and see the count drop. (Worker must be running — see the run guide.)

## Scenario 2 — The referral path (the underwriting workbench end-to-end)

This is the richest flow; it exercises the whole Underwriting module.

**Part A — Persona: Casey Customer**
1. Create + submit a new submission, generate a quote — this time the rating answers that imply
   higher risk produce status **Referred** instead of Quoted. (Generate a few submissions if
   needed; the simulated rating refers riskier profiles.)

**Part B — Persona: Uma Underwriter** (second browser profile)
2. Log in → `/underwriting/quote-referrals` → ✅ the referred quote is in the queue with risk
   tier, premium, referral reasons, and an SLA due date.
3. **Assign to me** → ✅ the operation shows you as assigned. *(Known limitation, on the roadmap:
   two underwriters clicking simultaneously is not yet guarded by optimistic concurrency.)*
4. **Triage**: set priority + status + due date → ✅ reflected in the queue row.
5. Add a **work note** and a **follow-up task**, then complete the task → ✅ the **timeline**
   shows every step in order.
6. **Request AI review** → ✅ an advisory packet appears (summary, risk signals, control gaps,
   suggested questions). Note it *cannot* change the quote — that's a structural guarantee.
7. **Request evidence**: category e.g. `MfaPolicy`, title `MFA enforcement evidence`, description,
   due date → ✅ evidence request created.

**Part C — Persona: Casey Customer**
8. `/evidence-requests` → ✅ the request is listed with its due date.
9. **Respond**: respondent `Jane Applicant`, title `CISO`, response text, and attach 1–5 small
   files (PDF/PNG/TXT…, ≤10 MB each). → ✅ documents show **scan status**; clean files become
   downloadable. Try a 6th file or an unsupported type — expect a clear validation error.
10. The local scanner deterministically flags files whose name contains an "infected" marker
    (e.g. `eicar` in the filename) — upload one to see **Rejected**, then use
    **upload replacement** to supersede it. Rejected files stay visible for audit but can never
    be downloaded.

**Part D — Persona: Uma Underwriter**
11. Back in the workbench: ✅ evidence shows as responded; download a clean document; **accept**
    the evidence or record a review decision (`Satisfied` / `Insufficient` / `NeedsClarification`
    with reason). `Insufficient` sends the owner a remediation notification.
12. **Approve / Decline / Adjust** the referral (Adjust changes premium/retention with a reason).
    → ✅ the queue row leaves the pending list; the decision + audit trail are recorded.

**Part E — Persona: Casey Customer**
13. If approved/adjusted: accept the quote and bind, as in Scenario 1.
14. `/notifications` → ✅ the evidence and decision notifications arrived.

## Scenario 3 — Authorization boundaries (prove the fences hold)

1. As **Casey Customer**, browse to `/underwriting/quote-referrals` directly → ✅ blocked before
   the underwriting workbench mounts or calls the referral API. Customers can never underwrite.
2. As **Uma Underwriter**, open `/submissions/new` directly → ✅ blocked before the submission
   form mounts. Underwriters don't create customer business.
3. As **Adrian Admin**, do both → ✅ allowed everywhere (Admin is in every policy).
4. As **Charlie Adjuster**, open `/claims/adjudication` → ✅ allowed; but `/submissions/new` and
   `/underwriting/quote-referrals` → ✅ blocked by the route guard and API policy. Adjusters only
   adjudicate claims.
5. As **Casey Customer**, open `/claims/adjudication` → ✅ blocked (claimants file claims, they don't
   adjudicate); `/claims/new` → ✅ allowed.
6. Log out, hit `http://localhost:5223/api/v1/submissions` with no token (e.g. from PowerShell)
   → ✅ **401** before any business logic runs.

## Scenario 4 — Platform behaviors worth seeing once

| Behavior | How to see it |
|---|---|
| **Idempotency** | Repeat a create-submission POST with the same `Idempotency-Key` header (PowerShell) → same response, no duplicate row; reuse the key with a *different* body → **409** |
| **Rate limiting (M44)** | Flood any endpoint (a loop of ~50+ rapid anonymous POSTs with tightened config, or check `SecurityAndRateLimitingEndpointTests`) → **429** with `Retry-After` |
| **Security headers (M44)** | Browser dev tools → Network → any API response → `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, CSP, etc. |
| **Correlation** | Any API response carries `X-Correlation-ID`; the API logs the same id on every line for that request |
| **Outbox at work** | Stop the Worker, accept a quote → no notification appears; start the Worker → it arrives within ~5s. Nothing was lost — that's the transactional outbox |
| **Health probes** | `/api/v1/health/live` stays Healthy even if you stop Postgres; `/ready` flips to unhealthy |

## Scenario 4A — Policy journey and retained history

1. As **Casey Customer**, open `/notifications` → ✅ there are no All/Personal/Team tabs. As Uma,
   Charlie, or Adrian → ✅ role-capable inboxes retain those tabs.
2. Complete Scenario 1 through bind. Open the policy-bound notification → ✅ **View policy** opens
   `/policies/{id}`, not the source Submission.
3. Open `/policies` → ✅ all and only Casey's policies appear, with policy number, coverage state,
   dates, PHP premium, limit, and retention. Policy detail shows `Contractual status: Bound`
   separately from `Coverage Active/Scheduled/Expired`.
4. Follow **Open source submission** → ✅ the page simultaneously shows Submission `Submitted`,
   latest Quote `Bound`, and the related Policy coverage state. No Generate quote control appears.
5. If the Policy appears in the Claims policy-options endpoint, ✅ **File claim** is visible; otherwise
   it is omitted. There is no Policy delete control.
6. Create a second draft for the same company → ✅ it is created and a possible-duplicate warning is
   shown. Multiple legitimate applications remain allowed.
7. On a Draft, choose **Delete draft**, reject the confirmation once (nothing changes), then confirm
   → ✅ the draft disappears. On a Submitted application before acceptance, choose **Withdraw
   submission**, then repeat the same API request with the same idempotency key → ✅ status remains
   Withdrawn and only one withdrawal outbox event exists.
8. Try withdrawal after accepting/binding the Quote → ✅ `409`; the Submission, Quote, and Policy
   history remain intact.

## Scenario 5 — The claims lifecycle (Phase 3, end to end)

**Prerequisite:** a **bound policy** — do Scenario 1 first so Casey has one. Use two browser
profiles (Casey ↔ Charlie) for the hand-offs.

**Part A — Persona: Casey Customer**
1. Dashboard → **File a claim** (`/claims/new`) → pick your bound policy → incident type
   `RansomwareExtortion`, incident date **within the policy period**, a description, claimed amount
   e.g. `120000` → ✅ claim created, status **Filed**. (Try an incident date *outside* the policy
   period → clear rejection — the file-time policy check.)
2. `/claims` → ✅ your claim is listed (yours only); open the detail → timeline shows "Filed".

**Part B — Persona: Charlie Adjuster** (second profile)
3. `/claims/adjudication` → ✅ the claim is in the queue → **Assign to me** → status **UnderReview**.
   *(The M44.5 concurrency guard applies — a second adjuster assigning gets 409.)*
4. Set a **reserve** (e.g. `150000`) with a reason → ✅ recorded (confidential — Casey never sees it).
5. **Request information** ("Please upload the forensic report") → status **InformationRequested**.

**Part C — Persona: Casey**
6. `/claims/:id` → answer the question inline, and **upload** a document (PDF/PNG) → ✅ shows a scan
   status; a **Clean** file becomes downloadable. (Upload a file whose name contains
   `MALWARE-TEST-SIGNAL` → ✅ **Rejected**, never downloadable — the fail-closed scan gate.)
   Answering returns the claim to **UnderReview**.

**Part D — Persona: Charlie**
7. Download Casey's clean document (authenticated fetch). Then **Accept** with a settlement — try a
   number **above** the policy limit net of retention → ✅ rejected; a number **within** it → ✅
   status **Accepted**, paid amount recorded. (Or **Deny** — requires a reason category + narrative;
   empty → 400.) Then **Close** → ✅ status **Closed**, and any leftover reserve is auto-released
   (visible in reserve history).

**Part E — Notifications**
8. As **Casey**, `/notifications` → ✅ claim assigned/decided messages arrived. As **Charlie**,
   `/notifications` → **Team** tab → ✅ the filing shows in the **claims-operations** team inbox.

## Database spot-checks (optional)

Connect any SQL client to `localhost:5432` (`postgres`/`postgres`, database `liansureprotect`):

```sql
select id, company_name, status from public.submissions order by created_at_utc desc limit 5;
select id, status, premium, referral_reasons from public.quotes order by created_at_utc desc limit 5;
select type, processed_at_utc, provider_message_id, failed_at_utc from public.outbox_messages order by created_at_utc desc limit 10;
select * from underwriting.quote_evidence_requests order by requested_at_utc desc limit 5;
select recipient_user_id, type, read_at_utc from notifications.notification_inbox_entries order by created_at_utc desc limit 10;
```

## When something looks wrong

1. Check the API terminal — errors log with the request's correlation id.
2. Check the Worker terminal — outbox failures log with retry metadata; a poison message shows
   `failed_at_utc` set in `outbox_messages`.
3. Auth issues → the troubleshooting table at the end of [Running The App](running-the-app.md).
4. UI design feedback or bugs you find while walking these scenarios: note the route, persona,
   and correlation id — that triple makes any issue reproducible.
