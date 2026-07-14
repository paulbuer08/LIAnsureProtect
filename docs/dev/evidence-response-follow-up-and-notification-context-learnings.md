# Evidence Response Follow-up and Notification Context Learnings

**Date:** 2026-07-14
**Branch:** `feat/evidence-follow-up-and-notification-context`
**Starting point:** synchronized `main` commit `7a191b7`
**Design source:** `docs/dev/evidence-response-follow-up-and-notification-context-design.md`

## Outcome

This slice closes four usability and trust gaps found during the Customer walkthrough:

1. a customer can add evidence while a response is still waiting for its first Underwriting review;
2. every response has a named, contactable person and an optional place for concerns or caveats;
3. notifications from multiple Submissions are grouped by Company and immutable Submission reference;
4. the unread badge refreshes while the user remains in the app, and opening an actionable notification marks it read.

The implementation does **not** claim that an email address, phone number, written answer, or uploaded
file proves a cyber control. Those are inputs to an Underwriter's decision. Automated file screening
answers “is this file safe enough to inspect?” and advisory plausibility answers “does this look broadly
consistent?” Neither answers the legal/business question “is this control truly operating?”

## The central model: an audit notebook, not an editable sticky note

Before this slice, an evidence request stored only the latest response fields on the request row. That
works for a single answer, but it cannot safely support later messages: changing the same columns would
erase what the customer originally said.

The new model keeps the request row as a **latest-state index** for existing queues and adds an immutable
`quote_evidence_responses` history:

```mermaid
flowchart LR
    ER["Evidence request\ncurrent status + latest snapshot"]
    R1["Initial response\nname, role, email, narrative"]
    R2["Follow-up\nconcern, message, or file"]
    R3["Remediation response\nafter an unfavorable review"]
    D1["Evidence document"]
    D2["Evidence document"]

    ER --> R1
    ER --> R2
    ER --> R3
    R1 --> D1
    R2 --> D2
```

Each response records:

- response ID, request ID, Quote ID, Submission ID, owner ID, and authenticated responder ID;
- respondent name, title/role, required email, optional phone;
- optional `Other concerns` alongside the main evidence narrative;
- kind (`Initial`, `FollowUp`, or `Remediation`) and UTC timestamp.

Documents now have an optional `evidence_response_id`, so a reviewer can tell which response introduced
a file. Older documents remain valid with a null response link. Existing response snapshots are not
invented or backfilled: audit-history entries start when the migration is deployed.

## State rules

| Current request state | Customer action | Result |
|---|---|---|
| `Open` | Submit the first response | Adds `Initial`; request becomes `Responded` |
| `Responded` + `NotReviewed` | Add message, concern, or file | Adds `FollowUp`; original response remains |
| `Responded` + `Insufficient` / `NeedsClarification` | Answer requested remediation | Adds `Remediation`; review returns to `NotReviewed` |
| Reviewed `Satisfied`, `Accepted`, or `Cancelled` | Add more evidence | Rejected; the reviewed/closed audit boundary is preserved |

A pre-review follow-up must contain at least one meaningful addition: response text, `Other concerns`, or
a document. Respondent name, title, and valid email are required each time so every entry is attributable.
Phone remains optional because not every organization permits direct phone disclosure.

## Required versus optional documents

“Optional” was previously misleading for automatically generated control requests. Those requests exist
because a material rating assertion—such as implemented MFA, EDR, or mature backups—received credit and
needs verification. They now use `Required` at creation, and the migration corrects existing rows where
`requested_by_user_id = 'system-assurance-policy'` was still `Optional`.

Underwriters may still deliberately create:

- `Required` when an artifact must support the assertion;
- `Optional` when contactable testimony may be enough but a file could help;
- `NarrativeOnly` when a file would add no useful assurance.

A later pre-review follow-up does not have to repeat a required file already supplied with the initial
response. Remediation still obeys the request's document rule.

## Human verification and privacy boundary

The owner API and Underwriting API expose contact/history only through their existing authorization
policies. Another owner still receives 404, and the Notifications module receives only safe display
snapshots—not respondent phone/email or document contents.

An Underwriter can load an exact evidence request from the workbench and see response history, contact
details, concerns, and clean documents before recording a decision. Suggested operational checks include:

1. compare the respondent's organization/domain and role with known account information;
2. contact the respondent through an independently trusted channel when the assertion is material;
3. inspect current, clean documents and their scope/date/issuer;
4. record the review reason and remediation guidance;
5. never treat automated screening or contact presence as automatic approval.

Future production work may add verified-domain indicators, consent/retention wording, contact-verification
events, and specialist integrations. This slice deliberately does not send email/SMS or expose contact
data to a new provider.

## Notification context and read behavior

Quote and Evidence notification events now carry safe snapshots of Company name and immutable Submission
reference. The outbox mapper copies those attributes into the Notifications read model. The inbox groups
items using that context, so two Submissions for the same customer no longer appear as one undifferentiated
stream.

```mermaid
sequenceDiagram
    participant Q as Quote/Underwriting transaction
    participant O as Transactional outbox
    participant W as Worker
    participant N as Notifications module
    participant UI as React app

    Q->>O: event + submission reference/company snapshot
    Q-->>UI: business action completes
    W->>O: poll pending event
    W->>N: idempotent inbox projection
    UI->>N: GET unread-count on load, focus, or meaningful invalidation
    N-->>UI: authorized personal + role-gated team count
```

The first implementation used a small five-second foreground count poll. Manual review rejected the
continuous request pattern even though the payload was tiny. The final behavior has no timer: it loads
the count with the shell and refreshes after meaningful cache invalidation, Notifications navigation,
or window focus. The trade-off is explicit—without push delivery, a newly projected notification may
not change the badge while the user remains idle in the same foreground tab.

For actionable notifications, `View quote`, `Open evidence request`, `View policy`, and similar links mark
the entry read before navigation. React Query applies an optimistic count/list update, removes the entry
from an Unread-only result, and then refetches authoritative state. Standalone `Mark as read` actions are
removed across the inbox so there is one unambiguous read behavior.

## Persistence and API changes

- Migration: `20260714121633_AddEvidenceResponseHistoryAndContacts`
- New table: `underwriting.quote_evidence_responses`
- New request columns: `respondent_email`, `respondent_phone`, `other_concerns`
- New document column: `evidence_response_id`
- Owner response contract: required name/title/email; optional phone/response/concerns depending on state
- Underwriter exact read: `GET /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}`
- Count-only inbox read: `GET /api/v1/notifications/unread-count`

## Boundary decisions

- Underwriting owns Evidence request, response, review, and document relationships.
- Notifications owns unread state and role-aware team visibility.
- Cross-context display context travels in events/outbox snapshots; Notifications never queries Submission
  or Underwriting tables to decorate an inbox row.
- A notification refresh is eventually consistent; no synchronous “write the other module now” shortcut
  was added.
- Existing owner/team authorization is applied before search, grouping, or counting.

## Verification

The release-sized verification gate passed:

- `dotnet build LIAnsureProtect.slnx --no-restore`: 0 warnings, 0 errors;
- standalone backend: 213 Unit tests and 274 Integration tests passed, with 4 intentional opt-in skips;
- `SubmissionDbContext`, `NotificationsDbContext`, `UnderwritingDbContext`, and `ClaimsDbContext`:
  no pending model changes;
- frontend TypeScript, ESLint, production build, and all 101 tests passed;
- fresh Docker local CI: all migrations applied, 213 Unit tests and 275 Integration tests passed,
  3 intentional external-service tests skipped, all 101 frontend tests passed, and Docker resources
  were cleaned up;
- artifact: `TestResults/local-ci-20260714-213918.zip`.

Acceptance coverage includes:

- initial, pre-review follow-up, remediation, and closed/reviewed state rules;
- immutable response history visible to owner and Underwriter;
- required automatic assurance documents and compatible manual document modes;
- another-owner 404 behavior;
- role-aware unread count, notification context attributes, grouping, and automatic read-on-open;
- zero-warning build, all four pending-model checks, backend tests, frontend type/lint/test/build, and
  Docker-backed local CI.

## Operational lesson

Trust is layered. A contact field improves **traceability**; a document improves **support**; malware
screening improves **safety**; advisory analysis improves **review efficiency**; and an Underwriter's
recorded decision supplies **accountability**. Calling any one layer “proof” would overstate what the
system knows.
