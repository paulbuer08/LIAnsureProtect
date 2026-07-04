# Claims Branch — Final Merge Checklist

> **Purpose:** the exact, mechanical list of steps to execute when `feat/claims-context` merges
> into `main` (after Phase 2 completes). The branch deliberately never touched the Tier-1 living
> docs (see `claims-status.md`); this checklist is where that debt is repaid in **one
> consolidation milestone** on a normal `main`-targeted PR.

> **See also:** [post-cm8-audit.md](post-cm8-audit.md) — the full-branch re-review; its three
> fixes are already merged, and its recorded findings appear as items below.

## 0. Pre-merge verification

- [ ] Re-run the **dry-run merge** (it was clean as of CM8, when `main` was still at `c490278` —
      Phase 2 will have moved it):
      ```bash
      git fetch origin
      git checkout -b temp/dry-run-merge feat/claims-context
      git merge origin/main --no-commit --no-ff   # resolve/record any conflicts, then:
      git merge --abort
      ```
      Expected conflict surface (thin by design): `Program.cs` module registrations,
      `ci.yml`/`update-database.ps1` migration steps, `AuthorizationPolicies.cs`/
      `ApplicationPolicies.cs` additions, `ProjectReferenceBoundaryTests` reference lists,
      `LIAnsureProtect.slnx`, `App.tsx` routes, `DashboardPage.tsx` cards,
      `NotificationTeamAudiences`/`NotificationAudiences`/`NotificationMessageTypes`,
      `NotificationInboxProjector`. All are additive line insertions — take both sides.
- [ ] Merge `origin/main` into `feat/claims-context`, resolve, full backend + frontend suites
      green locally, push, CI green on the branch.
- [ ] Apply the five Claims migrations against the local database
      (`scripts/update-database.ps1`) and smoke the app end-to-end once
      (file → assign → info round trip → documents → reserve → accept → close) using the
      manual-testing personas.
- [x] **Document downloads (audit finding, shared with evidence): DONE 2026-07-04.**
      Authenticated fetch → blob via the shared `lib/documentDownload.ts` helper; evidence +
      underwriting pages fixed on main (PR #52), claims pages on this branch. M47 presigned
      URLs remain the long-term replacement.
- [ ] **Auth0 tenant (audit finding):** verify the Action adds the namespaced roles claim
      (`https://liansureprotect.local/roles`) to **ID tokens** — `RequireRole` reads it in the
      SPA; without it, signed-in users are blocked from the claims pages.
- [x] **Body-size limits (audit finding, shared with evidence): DONE 2026-07-04 (PR #52).**
      Kestrel `MaxRequestBodySize` raised to 60 MB in the API host, commented against the 50 MB
      governance rule.

## 1. Open the consolidation PR (base `main`, the branch's only main-targeted PR)

Title suggestion: `Claims Bounded Context (CM1–CM8) — Phase 3` — squash or merge per repo
convention at that time.

## 2. Tier-1 living-doc updates (all inside that PR)

- [ ] **Encyclopedia — new Chapter 12 "Flow: Claims"** (`docs/encyclopedia/12-flow-claims.md`):
      compress the eight `docs/claims/cm*-design.md` docs into the chapter shape used by
      Chapter 8 — sub-flows A–F (FNOL + policy snapshot, adjuster queue/assignment with the
      M44.5 claim, information loop, scan-gated documents, reserves/financials with the
      confidential-reserve rule, decision/settlement guardrails + events), a state diagram, the
      settlement-cap and no-decision-without-assignment guardrails, and a scenario walk-through.
      Add it to `docs/encyclopedia/README.md`'s table of contents.
- [ ] **Encyclopedia Chapter 3 (architecture):** add the Claims module to the solution map
      (schema `claims`), the module list, and the health-check row; note the fourth outbox
      source.
- [ ] **Encyclopedia Chapter 10 (notifications):** add `ClaimsOutboxSource` to the pipeline
      diagram, the seven claim message types, and the `claims-operations` team audience row.
- [ ] **Build History (`docs/build-history.md`):** add an **Era 7 — "The Claims context
      (CM1–CM8)"** section (before/what/why per milestone — source: `claims-changelog.md` and
      the per-milestone learnings), and update the era timeline diagram + "Where the story goes
      next".
- [ ] **`CHANGELOG.md`:** fold in the eight entries from `docs/claims/claims-changelog.md`
      (keep the CM numbering; note they merged to main as one consolidation).
- [ ] **`docs/project-status.md`:** record the Claims context as delivered, the five `claims`
      migrations, the new policies/audience, and the next-step pointer.
- [ ] **Roadmap (`docs/dev/production-transformation-roadmap.md`):** mark `Claims` delivered in
      the bounded-context target list; note ClaimsAdjuster activation.
- [ ] **`docs/business/user-roles.md`:** ClaimsAdjuster is live — update its row and the policy
      table with `Claims.File/Read/Respond/Adjudicate` + `Notifications.Read` membership.
- [ ] **Manual testing guide (`docs/guides/manual-testing-guide.md`):** activate the
      ClaimsAdjuster persona (Auth0 test user + role assignment) and add the claims end-to-end
      script (file → assign → info round trip → upload incl. `MALWARE-TEST-SIGNAL` rejection →
      reserve → accept-at-cap/deny → close → inbox checks incl. the claims-operations team tab).
- [ ] **Run guide (`docs/guides/running-the-app.md`):** mention the `claims` schema/context in
      the migration list if contexts are enumerated there.
- [ ] **Root `README.md` + `docs/README.md` (documentation map):** add the Claims feature bullet
      and map rows; the `docs/claims/` folder becomes the per-milestone archive for Era 7
      (banner-mark `claims-changelog.md` and `claims-status.md` as historical once folded in).

## 3. Post-merge

- [ ] Delete `feat/claims-context` (local + remote) once merged.
- [ ] Verify CI on `main` (the ClaimsDbContext migration step applies all five claims migrations there for the first
      time on push).
- [ ] Revisit the two recorded seams when the dispatcher envelope work happens:
      legacy Infrastructure → `Modules.Claims.Domain` and → `Modules.Underwriting.Domain`
      (both retire together).

## Deferred items recorded on this branch (future scope, not blockers)

- Personal notify-the-assigned-adjuster channel (response events already carry
  `assignedAdjusterUserId`).
- Claim reopening after close; real payment provider port (M19 shape); notification deep links;
  queue caching via `ICacheableRequest` if the claims queue ever gets hot (M44.5 precedent).
- Orphaned-blob janitor for uploads rejected after storage (shared with evidence, see the
  post-CM8 audit); the `evidence-documents/` storage-key prefix shared by claim documents
  (cosmetic; renaming would orphan existing files).
