# GitHub Repository, CI/CD, and Automation

This document records how the LIAnsureProtect GitHub repository is hosted, protected, and automated.
It complements `docs/dev/ci-cd-flow.md` (which describes the *planned* flow); the pieces below are what
is **actually implemented** on `github.com/paulbuer08/LIAnsureProtect`.

## Hosting and visibility

- Hosted at `github.com/paulbuer08/LIAnsureProtect`, **public**.
- Full history is on GitHub (this is the source of truth; per-commit SHAs are not tracked in docs).
- Commit identity uses a **private GitHub noreply email**; no AI-attribution text is added to commits or PRs.

## Branch model and the pull-request flow

Work follows **trunk-based development**: `main` is always green and is **never committed to directly**.
Every change goes through a short-lived branch and a pull request that CI must pass before merge.

```text
git switch main && git pull            # sync the trunk
git switch -c feat/x                    # short-lived branch
# ...work, commit, run targeted tests...
git push -u origin feat/x               # publish the branch
gh pr create --base main --fill         # open PR -> CI runs
# CI green ->
gh pr merge --squash --delete-branch    # merge into main (squash = linear history)
git switch main && git pull             # re-sync
```

### Branch protection on `main`

- Require a pull request before merging (0 required approvals so the solo owner can self-merge).
- Require status checks to pass: **Backend (build, migrate, test)** and **Frontend (build, lint, test)**.
- Require branches to be up to date before merging (strict).
- Block force pushes; restrict deletions; require linear history.
- Administrators are **not** forced through the rules (emergency valve); an `--admin` squash merge can
  bypass the "up to date" requirement for low-risk, already-green PRs (used to drain Dependabot bumps).

> Note on Dependabot/strict interaction: because PRs must be up to date, dependency PRs merge **one at a
> time** (each rebases onto the new `main`). npm bumps especially must be serial because they share
> `package-lock.json`. After draining a batch manually, validate with `npm --prefix src/LIAnsureProtect.Web ci`.

## Workflows (`.github/workflows/`)

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | push to `main`, PRs to `main` | **Backend job**: Ubuntu + `pgvector` service container, `dotnet restore/build`, EF `database update`, `dotnet test` (PostgreSQL opt-in test on). **Frontend job**: Node 24, `npm ci`, build, lint, `vitest`. Auth/connection values are non-secret CI env vars; a dummy HTTPS authority satisfies the API startup guard. |
| `labeler.yml` | `pull_request_target` | Auto-labels PRs by area (frontend, backend, tests, documentation, database, ci, scripts, dependencies) from changed files via `actions/labeler`. Mapping in `.github/labeler.yml`. |
| `claude-review.yml` | PRs (opened/synchronize) | Runs `anthropics/claude-code-action` to review PRs. Auth via the `CLAUDE_CODE_OAUTH_TOKEN` subscription secret; needs `id-token: write`. **Skips bot PRs** (`dependabot[bot]`, `github-actions[bot]`) and is **not a required check**, so a usage/auth failure never blocks a merge. Uses `pull_request` (not `pull_request_target`), so fork PRs can't read the secret — external PRs can't consume Claude usage. |

## Security and quality features

- **CodeQL** (default setup): scans **C#, JavaScript/TypeScript, and Actions** on every PR. Copilot Autofix
  is on (free for public repos; suggests fixes for CodeQL alerts — complements, not replaces, Claude review).
- **Secret scanning + push protection**: on.
- **Dependabot**: alerts, **security updates**, **malware alerts**, **grouped security updates**, and weekly
  **version updates** (`.github/dependabot.yml`). Version updates are **grouped** so minor/patch bumps arrive
  as ~one PR per ecosystem (nuget, npm, github-actions); **major** bumps stay as individual PRs for review.
- **Actions hardening**: default workflow token is read-only; external contributors' workflow runs require approval.
- **2FA** is enabled on the owner account.

### Code-review options and switching

- **Claude GitHub App** (active): automatic PR review via the workflow above; consumes the owner's Claude
  subscription usage (not extra cost, but counts against quota).
- **OpenAI Codex connector** (suspended): kept installed but access-suspended; can be re-enabled as a fallback
  if Claude usage is exhausted. There is no reliable way to auto-detect quota exhaustion and auto-switch
  (no usage API); switching is a manual toggle.
- **Copilot Autofix**: free on public repos; security-alert fixes only.

## Deferred / optional

- **CodeQL merge-gate ruleset** (optional): a branch ruleset with "Require code scanning results" (CodeQL,
  High+ severity) turns CodeQL from reporting into a merge blocker.
- **Trivy** at containerization (Docker/IaC scanning); **GitHub Pages** for a live frontend demo at a
  deployment milestone; **Environments** + environment secrets for real deploy credentials.

## Verifying the setup

```bash
gh repo view                                                        # public
gh api repos/paulbuer08/LIAnsureProtect/branches/main/protection \
  --jq '{pr_required:(.required_pull_request_reviews!=null), checks:.required_status_checks.contexts}'
gh pr list --state open                                             # backlog
gh run list --branch main --limit 5                                 # recent CI
```
