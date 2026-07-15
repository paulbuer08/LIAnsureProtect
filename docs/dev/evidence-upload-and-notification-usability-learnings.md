# Evidence Upload and Notification Usability Learnings

Date: 2026-07-15
Branch: `feat/evidence-upload-and-notification-usability`

## Why this follow-up exists

Manual testing found several small interactions that became frustrating when repeated: a selected file could not be removed without reloading the page, files could not be dropped onto the form, long Evidence response histories pushed the action form far down the page, and unread Notifications depended on a subtle background shade.

This slice improves those interactions without changing Evidence ownership, document retention, review authority, or the transactional outbox. It is a presentation and client-side form-safety change. Persisted documents and submitted responses remain immutable audit history.

## Decisions

### One upload interaction everywhere

The app now uses one controlled `FileDropzone` for every customer-facing file-selection surface:

- initial and follow-up Evidence responses;
- rejected/failed Evidence replacement uploads; and
- Claim supporting-document uploads.

The component supports the ordinary file picker and drag-and-drop. Every selected file appears as its own row with an accessible `Remove <filename>` button. Removing a row only changes the pending browser selection; it never deletes a document that has already been submitted.

The distinction is similar to removing a paper from an envelope before mailing it. Once the envelope has been submitted and retained as Evidence or Claim history, a different audited lifecycle would be required to supersede it.

Client checks provide quick feedback for unsupported extensions, duplicate selections, and the five-file selection limit. Server-side content type, size, count, malware-screening, and authorization checks remain authoritative because browser checks can be bypassed.

### Required documents do not repeat on a pre-review follow-up

An initial response to a `Required` Evidence request still needs at least one document. A remediation response requested after an Underwriter decision also follows the request's document rule. However, while the request is already `Responded` and the decision remains `NotReviewed`, a follow-up may contain any meaningful combination of:

- an Evidence response;
- `Other concerns`; or
- another document.

It does not require the customer to upload the initial document again. A regression test covers a text-only follow-up to a `Required` request.

### Optional-document copy is omitted, not the contract

The long `Document requirement: Optional` panel was removed from the owner detail page because it repeated general underwriting caveats and visually competed with the actual work. The `Optional` value and backend behavior remain unchanged. `Required` and `NarrativeOnly` requests continue to display explicit instructions because those instructions materially affect what the customer must submit.

### Response history is complete but bounded

Both the owner Evidence detail and the Underwriter's exact Evidence result show the complete append-only response history. Their history lists now have a maximum height with an internal scrollbar. The scroll region is keyboard focusable and has an accessible name, so the layout remains compact without hiding audit entries or making mouse input mandatory.

### Download affordance

Clean, downloadable Evidence and Claim document actions now use the conventional hand pointer. Non-downloadable documents remain plain status text. This is only a visual affordance; clean-only download authorization and screening rules are unchanged.

### Unread Notifications need text plus color

An unread Notification now has:

- an amber left border matching the header badge family;
- an amber-tinted background; and
- a visible `Unread` pill.

The text marker means the distinction does not rely on color perception alone. Opening an actionable notification still marks that exact notification read before navigating to its exact subject. Standalone `Mark as read` controls remain intentionally absent.

### A reassessment compares every saved control answer

Manual testing exposed a contract mismatch. The React page treated a detailed implementation answer, such as `Sensitive data is encrypted` or `Phishing-resistant MFA is used`, as a reassessment change. The server compared only the five high-level claimed states. The browser therefore enabled `Create reassessment`, but the API returned `quote.reassessment.no_changes` when only a detailed answer differed.

The server now compares both parts of each persisted control assertion:

1. the high-level claimed state, such as `Implemented`, `Partial`, or `InPlace`; and
2. the assertion's structured `DetailsJson`.

JSON is compared semantically rather than as raw text, so a harmless property-order difference cannot create a new Quote version. A genuine detailed-answer change can now create version N+1 even when the high-level status stays the same.

The high-level answer and details must still tell one coherent story. For example, `Incident response plan in place` means the plan must also be approved, current, tested, and have named roles. Unchecking one of those details while retaining `InPlace` is a contradiction, not a valid downgrade. The page now lists these contradictions beside the controls and disables submission until the user either restores the required detail or changes the high-level status to the truthful lower state. The Application validator remains the authoritative backstop.

A true reassessment therefore follows this sequence:

```text
Open latest Quote -> Reassess controls
    -> change at least one saved high-level or detailed answer
    -> keep the broad status and details internally consistent
    -> complete a new named attestation
    -> Create reassessment -> immutable Quote version N+1
```

Changing only the attesting person's name or title does not change the risk assertion and cannot create another Quote version.

### Optional phone fields still validate when supplied

`Respondent mobile number` and `Respondent telephone number` are optional, meaning an empty value is valid. Once the user supplies a value, it must be valid. Previously this rule existed in the button-enablement calculation and in the Underwriting domain, but the page showed only neutral helper text. A malformed value silently kept `Submit evidence response` disabled.

The page now renders immediate, accessible field feedback:

- invalid email, mobile, and landline inputs use a red border and `aria-invalid`;
- the helper text becomes an exact corrective example;
- a form-level alert tells the user to fix the highlighted contact details; and
- the button remains unavailable until the values are valid.

The API already used Data Annotations for required fields, email, and maximum lengths. It now also uses custom `PhilippineMobileNumber` and `PhilippineTelephoneNumber` validation attributes for both JSON and multipart response models. The generic `[Phone]` attribute was deliberately not used: it accepts a broad international set and cannot enforce the separate Philippine mobile and landline contracts. Underwriting-domain normalization remains authoritative after transport validation, so a browser or direct API caller cannot bypass the rule.

## Boundary and safety notes

- No database migration or cross-context event changed. The existing Evidence response shape is unchanged, but invalid non-empty Philippine phone values are now rejected earlier by the API validation layer.
- No module reads or writes another module's tables.
- No outbox, SignalR, notification projection, or connection-pool behavior changed.
- Persisted documents cannot be removed through the new selection `×`; only unsent local `File` objects can.
- Underwriters retain the same full contact, response, concern, and clean-document history used for human review.

## Verification checklist

- Select multiple supported files and remove any one before submission.
- Drop supported files onto Evidence, replacement Evidence, and Claim upload areas.
- Confirm unsupported dropped formats show a readable message and are not selected.
- Confirm an initial `Required` Evidence response still needs a document.
- Confirm a `Responded` + `NotReviewed` Required request enables `Send follow-up` after entering only a meaningful response or concern.
- Confirm long owner and Underwriter response histories scroll inside their panels.
- Confirm clean document download actions use the hand pointer.
- Confirm unread Notifications show the amber border and `Unread` text; opening one marks only that item read.
- Confirm changing only a detailed implementation answer enables a reassessment and creates version N+1.
- Confirm a high-level control claim that contradicts its details shows a local explanation and cannot be submitted.
- Confirm invalid non-empty respondent email, Philippine mobile, and Philippine landline values show exact inline corrections and remain rejected by the API if the browser is bypassed.

Vitest is capped at four workers. Unrestricted parallelism on the Windows clean runner exhausted enough CPU that an unchanged five-second interaction test timed out and a separate `userEvent.type` sequence interleaved characters. Running the complete suite with four workers passed all tests without changing a timeout, assertion, or product behavior. The cap makes the default `npm run test` and Docker-backed CI use the verified stable configuration.

## Verification evidence

- `dotnet build LIAnsureProtect.slnx --no-restore`: passed with 0 warnings and 0 errors.
- Standalone backend tests: 219 Unit tests and 280 Integration tests passed; 4 external-service tests remained intentionally opt-in.
- EF pending-model checks: clean for `SubmissionDbContext`, `NotificationsDbContext`, `UnderwritingDbContext`, and `ClaimsDbContext`.
- Frontend: TypeScript and ESLint passed; the production bundle completed; all 110 tests passed.
- Docker-backed local CI: recreated PostgreSQL and Redis, applied all four migration sets, passed 219 Unit tests and 281 Integration tests with 3 expected service skips, passed the frontend build/lint/all 110 tests and API smoke checks, created `TestResults/local-ci-20260715-233524.zip`, and removed both containers, the database volume, and network.

The production build continues to print Rolldown's `INVALID_ANNOTATION` diagnostic for two upstream `@microsoft/signalr` pure comments. Rolldown explicitly ignores those comment annotations, transforms the package, and exits successfully; this slice does not patch installed third-party code.
