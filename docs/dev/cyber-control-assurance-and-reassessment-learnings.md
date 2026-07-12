# Cyber Control Assurance and Quote Reassessment — Learning Notes

## What changed

This slice replaced a walkthrough assumption—"the customer selected a checkbox, therefore the control
exists"—with an auditable assurance journey. Quote generation is still fast, but the system now records
what was claimed, why proof is required, what proof was reviewed, and whether the quote is eligible for
acceptance.

## The most important modeling lesson

A Submission status, Quote status, Policy status, and assurance status answer different questions:

| State | Question |
|---|---|
| Submission | Where is the insurance application intake? |
| Quote | What is the commercial offer lifecycle? |
| Policy | Is there a contract and when is coverage active? |
| Assurance | How strongly have material security claims been verified? |

Combining them would create misleading transitions—for example, changing a commercially `Quoted` offer
to an invented `EvidencePending` quote status. The implementation instead adds `QuoteAssuranceStatus`
alongside `QuoteStatus`.

## Self-attestation is useful, but it is not proof

The attestation records actor id, name, title, wording version, and timestamp. That makes the answer
accountable and reproducible. It does not make it true. Think of it as signing a customs declaration:
the declaration is important evidence about who said what, but inspection is a separate activity.

Five `ControlAssertion` rows are written for each quote version:

- MFA;
- Endpoint Detection and Response;
- Backup and Recovery;
- Incident Response Plan;
- Sensitive Data.

Each contains the normalized broad state plus a JSON implementation snapshot. This prevents later UI
copy or questionnaire evolution from changing the historical meaning of an old quote.

## Broad labels needed measurable detail

"MFA implemented" was too vague. It now asks whether privileged access, email, remote access, and the
workforce are covered and whether phishing-resistant factors are used. Similar detail exists for EDR
coverage/monitoring/tamper protection, backup immutability/credentials/restore testing/RPO/RTO,
incident-plan approval/update/exercise/roles, and sensitive-data inventory/encryption/types/volume.

The validator rejects internally inconsistent positive claims. For example, `Implemented` EDR cannot
coexist with 10% coverage and no monitoring. This is not external verification; it is a form-quality
guard that stops obviously contradictory answers from reaching rating.

## Evidence is risk-based

The deterministic policy requests proof for positive controls that receive material rating credit and
for sensitive/material combinations. Weak or Unknown answers receive conservative risk treatment
without demanding proof that a control is absent. This avoids making every small customer upload a
large audit pack before receiving any useful result.

Automatic requests are category-specific and plain English. They are created in Underwriting only
after the committed `QuoteGenerated` outbox event is dispatched.

## Two directions, two idempotency ledgers

```text
Quoting QuoteGenerated
  -> submission outbox
  -> QuoteAssuranceProjector
  -> Underwriting evidence requests

Underwriting EvidenceAccepted / RemediationRequired
  -> underwriting outbox
  -> QuoteAssuranceDecisionProjector
  -> Quoting assurance summary
```

Each direction has a table keyed by the source outbox-message id. A replay therefore no-ops. This is
important because a single satisfied decision must never be counted twice after a Worker retry.

Events carry ids and the fact. The Underwriting projector reads the detailed requirements through the
Underwriting-owned `IUnderwritingQuoteContextReader` port. Neither module reaches into the other's
schema, and there is no distributed transaction.

## Acceptance fails closed

`EvidenceRequired` and `Rejected` assurance block `Quote.Accept`. Only a self-attested quote with zero
requirements or a fully `Verified` quote can proceed, assuming its commercial state is also eligible.
Binding already requires `Accepted`, so it inherits the gate without duplicating logic.

## Reassessment is versioning, not regeneration

The old implementation returned the existing quote forever. Reassessment now requires explicit intent,
a changed assertion, and a new attestation. It creates version N+1 with `SupersedesQuoteId` and marks N
`Superseded` in the same Quoting transaction. Old provider attempts, evidence, decisions, and terms stay
queryable. Claimed improvements force evidence even if a base materiality rule would not.

Accepted/bound quotes reject reassessment. Once a contract exists, changing controls is an endorsement
or renewal concern with different legal and Claims implications.

## Automation stops at advice

The local scanner still makes the authority-bearing security decision: malicious/failed files cannot
be downloaded or used for review. For clean files it now records:

- assessment version;
- `Plausible` or `NeedsReview`;
- expected-category-term/date/version observations;
- `NeedsHumanReview` claim-consistency status.

It deliberately cannot set `HumanVerified`, accept evidence, alter premium, approve/decline a quote, or
bind a policy. A document can be malware-clean but substantively irrelevant; those are separate axes.

## Production follow-ups

- Legal/compliance must approve final attestation and disclosure wording.
- OCR/document extraction should replace simple local readable-text heuristics.
- Read-only Entra/Okta/Auth0, EDR, backup, and data-discovery connectors can raise assurance strength.
- Connector results need source identity, scope, collection time, expiry, and revocation semantics.
- A future Risk Engineer/Security Reviewer role may assist; the Underwriter remains the insurance
  decision owner.
- Post-bind changes need endorsement/renewal rather than quote reassessment.

## Verification

- `dotnet build LIAnsureProtect.slnx --no-restore`: 0 warnings, 0 errors.
- Full backend: UnitTests 203 passed; IntegrationTests 265 passed, 4 intended opt-in skips.
- Submission, Notifications, Underwriting, and Claims pending-model checks: clean.
- Frontend TypeScript, ESLint, production build: clean; Vitest 89/89 passed.
- Docker-backed local CI: passed against fresh PostgreSQL after applying every migration. It passed
  UnitTests 203, IntegrationTests 266 with 3 intentional external-service skips, frontend build/lint,
  all 89 frontend tests, artifact creation, and Docker cleanup. Artifact:
  `TestResults/local-ci-20260712-050920.zip`.
