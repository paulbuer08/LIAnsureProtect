# Manual-retest hardening batch — collection plan

**Status:** collecting approved findings; implementation has not started.

**Collection rule:** add related manual-testing findings here until the user decides that the batch is
large enough to implement together. Recording an item does not authorize a partial implementation.
When the batch is approved, create a dedicated branch from synchronized `main`, re-audit every item
against the then-current code, turn the accepted entries into phased tasks, and run the repository's
complete verification and protected-main workflow.

## Collected item 1 — respondent email deliverability and verification

### Observed gap

Evidence-response contact validation currently proves only that an address has a plausible shape.
The browser checks a small `local@domain.tld` pattern; ASP.NET Data Annotations use `[EmailAddress]`;
and the Underwriting domain parses the value with `MailAddress.TryCreate`. These layers correctly reject
malformed syntax, but they do not prove that the domain accepts email, that the mailbox exists, or that
the respondent controls it.

Manual testing demonstrated the distinction:

- `yahee.com` publishes a null MX (`MX 0 .`), which explicitly declares that the domain accepts no mail;
- `yah.com` currently has address records but no explicit MX. SMTP permits an implicit A/AAAA fallback,
  so absence of MX alone is not a safe universal rejection rule; and
- a syntactically valid address at either domain currently enables and completes an Evidence response.

Standards and platform references:

- [RFC 7505 — Null MX](https://www.rfc-editor.org/info/rfc7505/)
- [RFC 5321 section 5 — SMTP address resolution and implicit MX](https://www.rfc-editor.org/rfc/rfc5321.html)
- [ASP.NET Core model validation](https://learn.microsoft.com/aspnet/core/mvc/models/validation)
- [.NET email-validation guidance](https://learn.microsoft.com/dotnet/standard/base-types/how-to-verify-that-strings-are-in-valid-email-format)

### Approved product behavior

Use progressive validation rather than a provider allowlist:

1. Keep immediate browser syntax feedback.
2. Keep authoritative server syntax and length validation.
3. Add an asynchronous server-owned DNS mail-capability check.
4. Reject a nonexistent domain and a null-MX domain with user-safe, field-specific guidance.
5. Do not reject solely because explicit MX is absent when resolvable A/AAAA fallback exists. Accept the
   response but label the contact domain as unverified.
6. For likely misspellings of common providers, show a non-blocking suggestion such as
   `Did you mean yahoo.com?`. Never rewrite the user's value automatically and never restrict legitimate
   private-company domains to a public-provider allowlist.
7. DNS capability is not identity proof. Add an email verification link or one-time code before the
   respondent address is presented to Underwriting as verified.
8. Preserve the Evidence response even while contact verification is pending. Underwriting must see a
   clear `Unverified`, `Verification pending`, `Verified`, or `Undeliverable` contact state and must not
   treat the address as proof by itself.

### Implementation boundary for the future batch

- Do not put DNS/network access inside a Data Annotation, React render path, or Underwriting domain
  entity. Validation attributes and domain rules remain deterministic and synchronous.
- Define the asynchronous checking port in the owning Underwriting Application boundary and implement
  DNS resolution in Infrastructure. The browser remains advisory; the API is authoritative.
- Use short timeouts, cancellation, bounded positive/negative caching informed by DNS TTL, and
  low-cardinality structured diagnostics. A transient DNS outage must not erase the form or silently
  classify a domain as permanently invalid.
- Reject only authoritative negative results such as NXDOMAIN or null MX. Model timeout/SERVFAIL as a
  retryable or unverified result rather than a permanent validation failure.
- Do not use SMTP `VRFY`, mailbox probing, or third-party address-enrichment services. They are unreliable
  and introduce privacy, abuse, vendor, and data-processing concerns.
- Verification tokens must be single-use, short-lived, hashed at rest, owner/request scoped, auditable,
  rate-limited, and free of respondent or Evidence data in URLs, logs, SignalR hints, or Notifications.
- Any cross-context notification continues through the transactional outbox and idempotent projector;
  no module writes another context's tables.

### Acceptance scenarios

1. `person@yahee.com` is rejected with an inline message explaining that the domain declares it cannot
   receive email; the same request sent directly to the API is also rejected.
2. An NXDOMAIN address is rejected without persisting an Evidence response.
3. A valid business domain with MX records is accepted and initially marked unverified.
4. A resolvable no-MX domain with A/AAAA fallback is not falsely rejected; it is visibly unverified.
5. A likely `yahoo.com` misspelling receives a suggestion, but the user retains control of the value.
6. DNS timeout/SERVFAIL produces safe retry/unverified behavior and no raw resolver error reaches the UI.
7. A successful verification challenge records verified state, actor/address identity, and UTC time;
   replayed, expired, wrong-owner, and wrong-request tokens fail safely.
8. Changing a verified address resets verification and requires a new challenge while preserving prior
   Evidence-response audit history.
9. Underwriting screens clearly distinguish contact verification from Evidence truthfulness and never
   convert email verification into an automatic evidence decision.
10. Unit, API integration, frontend accessibility, DNS-adapter, rate-limit, cache, and full local-CI
    coverage pass without weakening module-boundary or existing Evidence tests.

### Re-audit questions before implementation

- Should an initial Evidence response remain submittable when DNS is temporarily unavailable, or should
  only the contact-verification part remain pending? The recommended default is to preserve the response.
- Which sender/domain will deliver verification messages in each environment, and is local email capture
  required for automated tests?
- Is verified contact state stored on each immutable response, on the Evidence request's current contact,
  or in a future Accounts/Contacts context? Preserve response history regardless of the chosen read model.
- What retention and resend limits will Legal/Compliance approve for respondent contact verification?

## Collected item 2 — subject-aware notification acknowledgement from every entry path

### Observed gap

The Notifications page marks an unread entry as read before navigating to its subject. The Evidence
request list uses an ordinary link to the same exact detail route, while a bookmarked/direct detail URL
only loads the Underwriting-owned request. Consequently, a customer can open and even respond to
`Verify incident response readiness` while its `Evidence requested` Notification remains unread and the
header badge remains inflated.

This is not a conflict between Evidence status and Notification status: `Responded` answers what happened
to the Evidence workflow, while `Unread` records whether the recipient has acknowledged the message.
The bug is that acknowledgement is tied to one navigation origin instead of the subject being viewed.

### Approved product behavior

1. Successfully opening an exact active subject acknowledges its applicable active Notification whether
   navigation started from Notifications, Evidence, a dashboard/deep link, a bookmark, or a direct URL.
2. Loading, unauthorized, not-found, and failed subject pages do not mark anything read. A resource is
   acknowledged only after its owner-scoped detail read succeeds.
3. Responding to an Evidence request is definitive acknowledgement even for a non-browser API client.
4. Acknowledgement is subject-aware and recipient-scoped. Opening one Evidence request must not read the
   other requests created for the same Quote or Submission.
5. If several active messages concern the exact same subject, viewing the subject acknowledges entries
   that occurred at or before the view time. A genuinely newer update projected afterward remains unread.
6. Historical/superseded Notifications retain their original read audit state and remain excluded from
   active unread counts; subject acknowledgement must not manufacture a historical read timestamp.
7. The badge and inbox refresh immediately after acknowledgement. Existing focus/navigation refresh
   remains a safety net, and payload-free SignalR remains only a refresh hint.
8. The same reusable behavior should be audited for Quote, Policy, Claim, Submission, and reassessment
   subjects. Apply it only where the exact destination actually reveals the notification's subject.

### Implementation boundary for the future batch

- Keep Evidence GET endpoints safe and cacheable. Do not add a write side effect to
  `GET /api/v1/evidence-requests/{id}`.
- Add an explicit idempotent Notifications command/API addressed by normalized subject type and exact
  subject id. Notifications remains authoritative for recipient-scoped read state; Evidence does not
  write the Notifications schema or reference its repository.
- After an exact resource query succeeds, a shared React hook acknowledges that subject and invalidates
  both inbox and unread-count TanStack Query caches. Guard the effect against rerenders and retries.
- Persist a recipient/subject `viewed-through` watermark in the Notifications context. Without it, a
  notification projected shortly after the customer opens the subject could become incorrectly unread
  because the acknowledgement arrived first. The idempotent projector applies the watermark only when
  the message occurrence time is not newer than the recorded view time.
- Consume the existing `QuoteEvidenceRequestRespondedDomainEvent` through the transactional outbox to
  acknowledge the owner's matching request notification. This covers API clients and makes response a
  durable proof of awareness without a cross-schema write.
- Use the existing exact subject reference (`evidence-request` plus Evidence request id), owner identity,
  event occurrence time, and outbox dedupe identity. Do not infer by title, category, Quote, company, or
  display text.
- Keep personal and team read models distinct. Re-audit team subject acknowledgement separately because
  team entries use per-user read receipts rather than one owner-specific `ReadAtUtc`.
- Publish/invalidate only after the Notifications transaction commits. Failures must surface as safe,
  retryable acknowledgement errors without preventing the user from viewing the resource.

### Acceptance scenarios

1. Opening an unread request from Notifications marks only that entry read and navigates to exact detail.
2. Opening the same unread request from the Evidence list marks the corresponding entry read and reduces
   the badge without visiting the Notifications page.
3. Visiting the owner-scoped detail URL directly or refreshing it produces the same idempotent outcome.
4. Submitting the first Evidence response marks the request notification acknowledged even when the
   caller never invoked the browser acknowledgement command.
5. Opening one of four Evidence requests changes only its exact subject; the other three remain unread.
6. Another owner's subject id returns 404/unauthorized and cannot change either owner's read state.
7. A subject acknowledgement committed before a delayed request-notification projection prevents the
   older message from later inflating the badge.
8. A new remediation or other update for the same subject occurring after the watermark remains unread.
9. Historical entries remain historical and retain their original read timestamp/null state.
10. Browser cache, multi-tab/focus refresh, SignalR reconnection, outbox replay, and duplicate commands
    converge to the same unread count without polling or double decrement.
11. Equivalent Quote, Policy, Claim, Submission, and reassessment routes are covered where their detail
    page proves that the corresponding update was viewed.
12. Unit, Notifications repository/projector, API integration, frontend routing/cache, accessibility,
    module-boundary, and full Docker-backed local-CI tests pass.

### Re-audit questions before implementation

- Should opening a subject acknowledge all older active messages for that exact subject or only the
  newest visible one? The recommended rule is `OccurredAtUtc <= ViewedThroughUtc` so later updates remain
  unread.
- Which non-Evidence destinations truly expose enough context to count as viewed? Do not acknowledge a
  notification merely because a broad list page was opened.
- Should a failed acknowledgement be retried silently after the detail remains usable, or receive a
  small non-blocking status message? It must never hide the resource or display raw diagnostics.

## Collected item 3 — capability-aware notification actions and Underwriting evidence deep links

### Observed gap

When a customer responds to an Evidence request, the outbox correctly projects an
`EvidenceRequestResponded` team notification for Underwriting operations. The Notification contains the
exact Evidence request id and Quote id, but the React action resolver treats every
`evidence-request` subject alike and always builds the customer route
`/evidence-requests/{evidenceRequestId}`. That route is intentionally restricted to Customer, Broker,
and Admin. An Underwriter therefore receives a relevant team update whose action can only lead to an
`Access restricted` page.

The authorization screen is behaving correctly and must not be weakened. The defect is the action
contract: a notification action was derived from subject type alone without considering the recipient's
authorized workflow.

### Approved product behavior

1. Underwriters continue to receive team notifications when a customer responds to Evidence. Suppressing
   the notification would hide work that Underwriting must review.
2. A Customer/Broker evidence notification opens the exact owner-facing Evidence request route.
3. An Underwriting team evidence notification opens the Underwriting workbench, selects the exact Quote,
   and opens or focuses the exact Evidence request through the Underwriter-authorized API.
4. Admin behavior follows its effective server-authoritative capabilities. It must not be inferred only
   from navigation visibility or untrusted token/UI state.
5. If the recipient does not have a usable action for that notification, the UI renders an informative,
   non-actionable update rather than a link that predictably produces 403/access-restricted.
6. Clicking a valid action marks that exact personal/team notification read only after the destination
   resource has been resolved successfully, consistent with collected item 2.
7. Server-side policies remain the security boundary. Role-aware routing improves correctness and user
   experience but never replaces authorization on the destination endpoint.
8. Titles for operational notifications must describe the event (for example, `Evidence response
   received`) rather than the generic `Notification`, so the Underwriter can understand the task before
   opening it.

### Implementation boundary for the future batch

- Introduce one typed notification-action resolver that accepts notification type, subject type/id,
  scope/audience, attributes, and the capabilities returned by `GET /api/v1/me`. Do not scatter role
  string checks across cards and pages.
- Preserve `evidenceRequestId` and `quoteId` in the notification contract. For an Underwriting team
  action, generate a stable deep link such as
  `/underwriting/quote-referrals?quoteId={quoteId}&evidenceRequestId={evidenceRequestId}`.
- Teach the Underwriting workbench to validate the query parameters, select the matching referral, and
  load the exact request through
  `GET /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}`. Invalid,
  stale, superseded, or unauthorized references must receive safe not-found/unavailable handling.
- Keep owner and Underwriting query handlers separate. Do not grant Underwriters access to
  `GET /api/v1/evidence-requests/{id}` and do not make the customer React route accept Underwriters.
- Keep team projection role-gated through the Notifications audience model. Add an automated contract
  check ensuring every actionable notification type/audience combination resolves to a route and API
  policy that audience can use.
- Apply the same audit to Quote, Submission, Policy, Claim, reassessment, and future subjects so no role
  receives an action that points at another role's workflow.
- Coordinate with collected item 2 so successful deep-link resolution acknowledges the team receipt,
  while a failed destination does not silently mark it read.

### Acceptance scenarios

1. A Customer responding to an Evidence request creates an Underwriting team notification with a clear
   `Evidence response received` title and the exact Quote/request context.
2. An Underwriter clicking that action remains inside the authorized Underwriting workbench, with the
   matching Quote selected and the exact Evidence request loaded.
3. The same Underwriter cannot access the customer-only Evidence detail API or page directly; existing
   403/access-restricted behavior remains intact.
4. A Customer/Broker notification for the same subject continues to open the owner-facing detail route.
5. Opening request A cannot select request B, even when both have the same category or company.
6. A stale, malformed, superseded, or nonexistent deep link cannot expose another owner's Evidence and
   does not mark the notification read.
7. A user without the required capability sees no unusable action; the update remains readable and the
   API still enforces its policy independently.
8. Admin and multi-role accounts resolve actions from their effective capabilities deterministically.
9. Personal/team tabs, unread counts, read receipts, SignalR refresh hints, and item-2 subject
   acknowledgement stay consistent after navigation.
10. Notification mapper/title tests, role/audience/action matrix tests, Underwriting API integration
    tests, React routing/deep-link tests, accessibility tests, module-boundary tests, and full
    Docker-backed local CI pass.

### Re-audit questions before implementation

- Should the Underwriting workbench open the Evidence panel inline or use a dedicated Underwriter-only
  Evidence detail route? Prefer the inline deep link while the current workbench owns all review actions;
  introduce a dedicated route only if the panel becomes too large or requires independently shareable
  navigation state.
- Which Admin capability should win when an Admin can perform both owner-support and Underwriting work?
  Base this on the notification's scope/audience plus effective capabilities, not a hard-coded role order.
- Which legacy notification rows lack Quote ids or meaningful titles? Define a safe no-action fallback
  rather than inferring identity from display text.

## Future collected items

Add later approved findings below this heading. Keep each entry independent enough to re-audit, estimate,
and either include or exclude when the grouped implementation milestone is authorized.
