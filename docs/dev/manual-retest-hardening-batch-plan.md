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

## Future collected items

Add later approved findings below this heading. Keep each entry independent enough to re-audit, estimate,
and either include or exclude when the grouped implementation milestone is authorized.
