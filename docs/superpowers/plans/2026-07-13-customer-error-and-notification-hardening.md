# Customer Error and Notification Hardening Implementation Plan

## Objective

Deliver the approved error-safety, reassessment cancellation, evidence-detail navigation, notification identity, and production observability slice without weakening tests or crossing module boundaries.

## Phase 1 — Public error contract and shared frontend client

1. Add typed application/HTTP business failures with stable public codes.
2. Add a centralized Problem Details factory/exception handler and include the sanitized correlation ID.
3. Expose `X-Correlation-ID` through CORS and align rate-limit failures.
4. Add a Zod-validated frontend `ApiError` parser and safe-message helper.
5. Migrate every feature API to the shared response/error boundary.
6. Add an application error boundary and privacy-safe telemetry interface.
7. Test known business failures, unknown failures, validation errors, correlation IDs, and the absence of raw JSON in rendered pages.

Verification: targeted API integration tests, frontend API tests, app/page tests, TypeScript and lint.

## Phase 2 — Reassessment cancellation and client-side change detection

1. Extract a canonical comparison of current quote control assertions and reassessment form state.
2. Disable reassessment creation until a control assertion changes.
3. Add immediate cancellation for a clean form.
4. Add the accessible `Discard reassessment changes?` confirmation for a dirty form.
5. Reset values, attestation, errors, and reassessment mode without changing the persisted quote.
6. Return `quote.reassessment.no_changes` from the server guard.

Verification: focused unit/component tests and quote endpoint conflict tests.

## Phase 3 — Evidence summary list and owner detail

1. Split owner summary and detail result contracts.
2. Add opaque cursor parsing/creation and bounded page size.
3. Add status/category/quote/overdue filters to the Application query and EF no-tracking reader.
4. Keep document loading out of the list query.
5. Add owner-scoped detail query that loads documents for one request only.
6. Add `GET /api/v1/evidence-requests/{id}` with `404` for missing/other-owner records.
7. Refactor the frontend into a paged summary page and one detail page.
8. Add `/evidence` compatibility redirect and fix every canonical link.

Verification: query/reader tests, integration ownership tests, frontend list/detail/loading/error/action tests.

## Phase 4 — Exact quote history and notification deep links

1. Add an owner-scoped quote-history/detail read within the existing submission ownership boundary.
2. Add exact quote route `/submissions/:submissionId/quotes/:quoteId` and select the requested immutable version.
3. Enrich quote-generated and evidence-request event snapshots with minimum safe display fields.
4. Preserve custom outbox mapping and make notification titles/body data-driven.
5. Link each evidence and quote notification to its exact subject.
6. Document and test multiple legitimate quote-ready notification history.

Verification: domain/outbox mapper tests, owner API tests, notification page tests, exact-route component tests.

## Phase 5 — Production observability contract

1. Enable JSON console logging for Production/Aws while retaining readable local development logs.
2. Add request-outcome metrics/logging with stable event IDs and error-code dimensions.
3. Add a disabled-by-default, privacy-safe browser telemetry adapter and configuration validation.
4. Record frontend unexpected errors and failed API outcomes without sensitive payloads.
5. Add a CloudWatch/RUM deployment runbook with log-group, metric-filter, alarm, sampling, privacy, and environment contracts.
6. Update the future Terraform acceptance criteria rather than provisioning unmanaged AWS resources here.

Verification: configuration tests, structured-log/metric tests where practical, frontend telemetry tests, documentation review.

## Phase 6 — Closeout documentation and full verification

1. Update `docs/project-status.md`, `README.md`, `CHANGELOG.md`, architecture overview, and production roadmap.
2. Add a detailed milestone learning note with diagrams, examples, commands, warnings, and final evidence.
3. Run `dotnet build LIAnsureProtect.slnx --no-restore` and require zero warnings/errors.
4. Run the full backend suite.
5. Run pending-model checks for Submission, Notifications, Underwriting, and Claims DbContexts.
6. Run frontend TypeScript, ESLint, all tests, and production build.
7. Run full Docker-backed local CI from a clean dependency state.
8. Keep changes local to the milestone branch until the user is ready for PR closeout.

## Commit boundaries

- `docs: plan customer error and notification hardening`
- `feat: centralize safe customer error handling`
- `feat: add cancellable quote reassessment`
- `feat: add evidence request detail navigation`
- `feat: add precise quote and evidence notifications`
- `feat: add production observability contract`
- `docs: close customer error and notification hardening`
