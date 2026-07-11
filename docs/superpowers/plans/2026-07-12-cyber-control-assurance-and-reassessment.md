# Cyber Control Assurance and Quote Reassessment — Implementation Plan

## Conventions

- Work on `feat/cyber-control-assurance-and-reassessment` only.
- Preserve module boundaries and the custom transactional outbox.
- Commit each coherent task after a zero-warning build and focused tests.
- Never weaken an existing test.
- Keep `.claude/` untouched and uncommitted.
- Use four pending-model checks: Submission, Notifications, Underwriting, and Claims.

## Phase A — Contract and language

1. Add attestation and detailed questionnaire contracts with validation.
2. Replace walkthrough language and explain provisional verification.
3. Add focused API/frontend tests for required attestation and questionnaire behavior.

## Phase B — Versioned Quoting assurance model

1. Add `ControlAssertion`, assertion/assurance enums, and deterministic evidence policy.
2. Add quote version, supersession, assurance summary, and attestation audit fields.
3. Persist via an additive SubmissionDbContext migration.
4. Extend quote reads/results without exposing persistence entities.
5. Add domain, handler, repository, migration, and endpoint tests.

## Phase C — Evidence requirement projection

1. Extend the Underwriting-owned quote-context port with assurance requirement snapshots.
2. Add an idempotent quote-assurance projector for `QuoteGeneratedDomainEvent`.
3. Create category-specific evidence requests in Underwriting, including quoted (not only referred)
   provisional risks.
4. Reuse existing evidence notifications, storage, scanning, and review history.
5. Pump the dispatcher in integration tests before asserting cross-context state.

## Phase D — Review feedback and acceptance gate

1. Consume Underwriting satisfied/remediation events into a Quoting assurance projector.
2. Update required/satisfied counts idempotently.
3. Block quote acceptance while required assurance is incomplete or rejected.
4. Surface assurance status, requirements, and evidence link in the customer detail page.

## Phase E — Automated advisory assessment

1. Extend deterministic evidence scanning with plausibility and claim-consistency findings.
2. Persist advisory findings with version/hash metadata.
3. Display findings to underwriters without granting them decision authority.
4. Prove by architecture/domain tests that automation cannot approve, decline, adjust, accept, or bind.

## Phase F — Versioned reassessment

1. Add explicit reassessment intent to the quote command/API.
2. Require changed assertions and repeat attestation.
3. Create quote version N+1 and supersede N in one Quoting transaction.
4. Require evidence for claimed improvements.
5. Reject reassessment after acceptance/binding and preserve all prior history.
6. Add customer UI and focused tests.

## Phase G — Documentation and verification

1. Update Tier-1 docs, manual testing guide, architecture overview, changelog, and README links.
2. Write detailed learnings including diagrams, tradeoffs, failure modes, and verification output.
3. Run zero-warning build, full backend tests, all four pending-model checks, frontend TypeScript/lint/
   tests/build, and Docker-backed local CI.
4. Open a protected-main PR, inspect CI/CodeQL/Claude threads, address findings, and squash-merge only
   when fully green.

