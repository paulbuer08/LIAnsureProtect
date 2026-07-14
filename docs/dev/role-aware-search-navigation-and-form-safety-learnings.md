# Role-aware search, navigation, and form safety — implementation learnings

**Status:** implemented, fully verified, and squash-merged through protected-main PR #64 as commit
`67334c9`; verification evidence is recorded in `docs/project-status.md`.

This record explains the product decisions behind the implementation, the boundaries that kept the
change safe, and the practical checks future work should repeat. The source design remains
`docs/dev/role-aware-search-navigation-and-form-safety-design.md`.

## 1. What changed

The slice joined five related usability problems that share one principle: the interface should
help a person find and leave work without weakening the system's audit or authorization rules.

1. Every Submission now has an immutable human reference such as
   `SUB-2026-8A9B7C6D5E4F3210`. The UUID remains the database identity.
2. Collection pages gained contextual, role-appropriate search and filters.
3. Deeper pages gained semantic breadcrumbs.
4. Draft-creation and Evidence-response forms gained safe cancellation.
5. Evidence requests now state whether a document is **Required**, **Optional**, or
   **NarrativeOnly**.

The result is intentionally not a global omniscient search box. Each search starts inside a
business context and inside the caller's existing authorization boundary.

## 2. Reference versus identity

The Submission ID and Submission reference point to the same record, but serve different audiences:

| Value | Example | Purpose | Mutable? |
|---|---|---|---|
| Submission ID | `1a31da08-10bd-4a4d-b87c-8a5d96ba87a5` | technical primary key, URLs, events, joins | No |
| Submission reference | `SUB-2026-8A9B7C6D5E4F3210` | spoken/written customer and operations reference | No |

The reference does not replace or contain the UUID. It is a unique alternate key generated when
the Draft is created. In database terms, both columns identify one row; in human terms, the
reference is the claim-check ticket and the UUID is the barcode encoded behind it.

The migration `AddSubmissionReference` backfills existing rows before adding the unique constraint.
This ordering matters: adding a non-null unique column with one shared default would make the second
legacy row violate uniqueness.

Submission references are copied as display snapshots into the Policy and Underwriting Evidence
read shapes. Cross-context aggregates are not shared and no cross-schema foreign key was added.

## 3. Search authorization matrix

Search is a narrowing operation, never an authorization operation.

```text
authenticated identity
        |
        v
policy / route authorization
        |
        v
owner or team scoped query          <- security boundary
        |
        v
search + workflow filters           <- usability narrowing
        |
        v
bounded response
```

| Surface | Roles | Searchable identity | Additional filters |
|---|---|---|---|
| Submissions | Customer, Broker, Admin | reference, exact UUID, applicant, email, company | status, created dates; stable cursor paging |
| Evidence requests | Customer, Broker, Admin | request/quote/submission identity, reference, company, title, description | status, category, review decision, document requirement, overdue |
| Policies | Customer, Broker, Admin | policy number/UUID, submission reference/UUID, applicant, company | contractual status, computed coverage state |
| My claims | Customer, Broker, Admin | claim number/UUID, policy number/UUID | claim status, incident type |
| Underwriting referrals | Underwriter, Admin | quote UUID, submission UUID, owner identity | risk tier, priority, assignment, evidence state; existing urgency filter remains |
| Claims adjudication | ClaimsAdjuster, Admin | claim/policy identity and assigned adjuster | status, assignment, open claimant questions |
| Notifications | all inbox roles | message type, subject identity, safe attribute snapshots | read state; Personal/Team scope only for team-capable roles |

Important consequences:

- A Customer cannot search another Customer's UUID and learn whether it exists.
- An Admin does not silently bypass owner scope on customer workflow pages. Admin sees the wider
  operational filters only on operational pages whose policy authorizes Admin.
- Customers and Brokers never receive assignment, SLA, reserve, or team-audience filters.
- The API and repository/query layer enforce scope even if a caller manually edits query-string
  parameters.
- Search text is capped at 200 characters and invalid filters return safe Problem Details rather
  than raw exceptions.

### Notification search and provider consistency

Notification titles are computed and attributes are stored as JSON. SQLite and PostgreSQL do not
offer identical case-insensitive string operators. The implementation therefore keeps recipient or
audience scoping plus structured read/type filters in SQL, then applies ordinal case-insensitive
free-text matching to that already-authorized projection before the 50-item response cap. This avoids
provider-specific behavior and, critically, never searches outside the caller's inbox.

### Referral-cache safety

The Underwriting referral queue retains one shared, unfiltered 10-second cache entry. A separate
`SearchQuoteReferralsQuery` narrows the complete cached result. Filter combinations are not cached
as independent keys, so the existing invalidation filter still has one key to evict and cannot leave
stale variants behind.

## 4. Evidence document requirements

Evidence requests now carry one deliberate contract:

| Requirement | Customer response | Server rule |
|---|---|---|
| `Required` | narrative plus at least one file | reject a response with zero documents |
| `Optional` | narrative; file may be added | accept with or without files |
| `NarrativeOnly` | narrative only | reject attached files |

Automatically generated control-assurance requests are `Required`. An Underwriter explicitly
chooses the requirement for a manually created request. The UI mirrors the rule, but the command
handler is authoritative so API callers cannot bypass it.

The migration `AddEvidenceRequestIdentityAndDocumentRequirement` safely backfills legacy evidence
requests with a bounded legacy Submission reference and `Optional` requirement, preserving the
earlier narrative/attachment-metadata workflow without unexpectedly blocking an open legacy request. It also persists a
company-name snapshot for clear list/search display. These are read/display snapshots; Underwriting
still does not own the Submission aggregate.

## 5. Safe form cancellation

### Create Draft

- Empty or whitespace-only input: Cancel immediately resets local form/mutation/idempotency state
  and replace-navigates to Dashboard.
- Any meaningful non-whitespace input: show the accessible confirmation modal.
- Confirm discard: reset all local state and replace-navigate.
- Keep editing: close the modal without changing the form.

The rule deliberately treats punctuation and digits as meaningful input too. A user may type a
legitimate company like `3M`; detecting only alphabetic characters would wrongly discard it.

### Evidence response

Dirty detection includes responder name, responder title, narrative, and selected files. Cancelling
never cancels the Underwriter's request; it discards only the Customer's unsubmitted local response.
Files selected by the browser are references in local page state until Submit succeeds, so discard
clears them without deleting an audit record or a stored document.

## 6. Breadcrumb semantics

Breadcrumbs describe the resource hierarchy, not the browser's accidental history:

```text
Dashboard / Submissions / SUB-2026-...
Dashboard / Evidence requests / Verify multi-factor authentication
Dashboard / Policies / LIP-CYB-...
Dashboard / Claims / CLM-CYB-...
```

Every linked segment has a stable route. The last segment is the current page and is not a link.
This means a notification deep-link still shows a truthful path even when the user did not visit
the collection page first.

## 7. Time presentation

UTC remains the persistence and API contract. Customer-facing list/detail pages format timestamps in
the browser's local timezone. Exact UTC can remain available in technical context/title text where an
operator may need it. Changing display formatting never changes stored instants.

## 8. Testing lessons

- Test owner scope and search together. A search test that uses only one owner cannot prove absence
  of cross-owner disclosure.
- Keep backend command tests for Evidence requirements even when the button is disabled in React.
- When filter options repeat a result label (for example `Active` or `UnderReview`), UI tests should
  locate the semantic result element rather than assume the word appears once.
- Test dirty Cancel with text and with selected files; the browser file input is part of unsaved
  state even if every text field is empty.
- Keep the zero-warning analyzer gate. It caught inefficient concrete return types and
  culture-sensitive string normalization during this slice.
- Pending-model checks must cover all four contexts because this change adds migrations to both
  Submission and Underwriting, while a snapshot drift in Notifications or Claims would still break
  a clean deployment.

## 9. Deliberate deferrals

- No global cross-context search endpoint. That would require an explicitly designed search read
  model, authorization projection, indexing, retention, and eventual-consistency contract.
- No user-editable alias. The immutable generated reference avoids naming collisions, rename audit,
  profanity/PII concerns, and uniqueness races while still solving human identification.
- No live AWS logging/search resources were provisioned. The existing Terraform milestones own
  CloudWatch, RUM, alarm, and deployment resources.
- No bound Policy delete or casual cancellation was introduced. The prior audit/Claims/notification
  lifecycle decision remains unchanged.

## 10. Useful implementation map

- Submission reference: `Domain/Submissions/Submission.cs`, Submission EF configuration/migration,
  submission list/detail results.
- Context search: controllers plus the corresponding Application query/reader; React feature API,
  TanStack Query hook, and page.
- Evidence requirement: Underwriting Domain `EvidenceDocumentRequirement`, evidence command handler,
  Underwriting migration, owner and Underwriter pages.
- Breadcrumbs: `src/LIAnsureProtect.Web/src/components/Breadcrumbs.tsx`.
- Friendly dates: `src/LIAnsureProtect.Web/src/lib/dateTime.ts`.

The durable rule for future work is simple: **first decide who may see the haystack, then allow that
person to search inside it**.
