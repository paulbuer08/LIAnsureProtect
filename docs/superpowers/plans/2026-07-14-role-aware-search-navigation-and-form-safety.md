# Role-Aware Search, Navigation, and Form Safety Implementation Plan

## Objective

Deliver human submission identity, contextual role-aware search, semantic breadcrumbs, friendly
timestamps, and safe form cancellation across the existing modular-monolith boundaries. Preserve the
already verified customer-error/notification work and deliver both through one protected-main PR.

## Phase 1 — Submission identity and owner collection

1. Add immutable server-generated `Submission.Reference` and a unique EF configuration/index.
2. Add a SubmissionDbContext migration that backfills every existing row deterministically before
   making the column required and unique.
3. Propagate the reference through create/update/list/detail results without replacing UUID routes.
4. Add owner-scoped search, status/date filters, opaque cursor pagination (20 default, 50 maximum),
   stable ordering, and invalid-filter Problem Details.
5. Add a role-appropriate Submission filter panel whose state is stored in URL parameters.
6. Render reference, friendly Created time, filtered empty state, Previous/Next, and exact detail link.
7. Add Draft creation Cancel with clean immediate exit, dirty confirmation, full form/mutation reset,
   and `replace` navigation.

Verification: domain/reference tests, migration/model tests, repository/query tests, API ownership and
cursor tests, Zod/form/list component tests, build 0/0.

## Phase 2 — Shared navigation and presentation primitives

1. Add accessible `Breadcrumbs` and local-time formatting helpers.
2. Replace list/create/detail `Back to ...` links across Submission, Quote, Evidence, Policy, Claim,
   Underwriting, Claims adjudication, and Notifications pages with semantic route breadcrumbs.
3. Use dynamic resource labels only after owner/role-scoped data loads; loading/error routes retain
   safe generic final labels.
4. Add focused component/accessibility tests and keep route-level lazy loading intact.

Verification: shared component tests, representative route tests, TypeScript and ESLint.

## Phase 3 — Evidence context, requirements, and owner search

1. Extend the Underwriting-owned quote-context snapshot with submission reference/company and persist
   those display snapshots on new evidence requests.
2. Add required evidence-document mode to domain, EF configuration, migrations, results, events, and
   underwriter/manual request commands; automatic material-control requests use Required.
3. Extend owner evidence search/filtering with search, review decision, and document requirement while
   preserving existing status/category/quote/overdue cursor behavior and lightweight list SQL.
4. Show submission reference/company/quote version on summary/detail and breadcrumb surfaces.
5. Add customer evidence Cancel response with text/file dirty detection and persisted-history copy.
6. Enforce meaningful name/title/narrative and request-specific document rules in both UI and domain.

Verification: domain tests, Underwriting migration/model check, owner/other-owner API tests, cursor and
filter tests, outbox/notification snapshot tests, frontend list/detail/form tests, build 0/0.

## Phase 4 — Other owner collection search

1. Policies: owner-scoped search by policy/reference/UUID/applicant/company and contractual/coverage
   filters; role-appropriate UI for Customer/Broker/Admin.
2. My Claims: owner-scoped search by claim/policy number or exact IDs plus status/incident filters;
   preserve Claims-module ownership and Claim-number identity.
3. Keep result contracts complete and no-tracking; do not add cross-context Claims reads.
4. Add filtered empty states, URL query state, friendly dates, and backend/frontend tests.

Verification: Application/module reader tests, authorization/ownership integration tests, component
tests, Claims and Submission pending-model checks, build 0/0.

## Phase 5 — Operational and notification search

1. Underwriting referrals: search the complete cached authorized queue by submission reference/UUID,
   quote UUID, company, and applicant; filter priority/risk/assignment/SLA/evidence state. Preserve
   cache correctness and write invalidation.
2. Claims adjudication: server-side search by claim/policy identities and owner id plus
   status/incident/assignment/open-information filters.
3. Notifications: server-side safe search/type/read filtering before the bounded read; Personal/Team
   scope remains derived from server roles, and Customer/Broker never receive team controls.
4. Admin receives the applicable filters on each authorized route but no implicit cross-owner bypass.
5. Add authorization, no-disclosure, cache, reader, and role-rendering tests.

Verification: focused backend/module/integration/frontend tests and build 0/0.

## Phase 6 — Documentation and closeout

1. Update `docs/project-status.md`, README, CHANGELOG, architecture overview, build history, production
   roadmap, running/manual-testing guides, and relevant encyclopedia chapters.
2. Add a rich learning note covering alternate identity, role/filter matrix, query/index choices,
   module snapshot seams, URL state, accessibility, examples, diagrams, and warnings.
3. Run zero-warning `dotnet build` and the full backend suite.
4. Run pending-model checks for Submission, Notifications, Underwriting, and Claims DbContexts.
5. Run frontend TypeScript, ESLint, all tests, and production build.
6. Run full Docker-backed local CI against fresh PostgreSQL and retain the passing artifact.
7. Commit each coherent phase with plain conventional messages and no attribution.
8. Push, open a PR into protected `main`, inspect every CI/CodeQL/review thread, address findings,
   squash-merge only when green, resync local `main`, prune/delete merged feature branches, and leave
   `.claude/` untouched.

## Commit boundaries

- `docs: plan role-aware search and navigation`
- `feat: add searchable submission references`
- `feat: add semantic navigation and safe form cancellation`
- `feat: add searchable evidence response requirements`
- `feat: add role-aware owner collection filters`
- `feat: add operational and notification search`
- `docs: close role-aware search and navigation`
