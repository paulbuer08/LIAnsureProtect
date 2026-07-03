# Milestone Documentation Practice

This project treats documentation as part of the implementation, not as an afterthought.

Each milestone should leave enough written context that a future session can continue without needing to replay the entire conversation.

## Required Documents To Update

For every meaningful milestone, update:

- `README.md` when the user-facing project status or documentation index changes.
- `CHANGELOG.md` when files, architecture, tests, or behavior change.
- `docs/project-status.md` for the current branch, milestone status, verification, next step, and decisions to remember.
- The **Tier-1 living documents** (see the [Documentation Map](../README.md)): the
  [Encyclopedia](../encyclopedia/README.md) chapters the milestone touches, the
  [Build History](../build-history.md) era table, and the guides
  (`docs/guides/`) when run/test behavior changes.
- Relevant files under `docs/architecture/` when architecture decisions change.
- Relevant files under `docs/dev/` when setup, tooling, verification, or workflow changes.

**Keep the doc count down:** each milestone may add its one design/learnings record to the
archive; any new *standalone* reference document needs a reason a Tier-1 living document cannot
hold the content. Superseded documents are banner-marked historical (if referenced by archive
records) or deleted (if not).

## Required Learning Notes

Every milestone with meaningful discussion should have a learning notes document.

Use this naming style:

```text
docs/dev/milestone-N-short-topic-learnings.md
```

Example:

```text
docs/dev/milestone-2-backend-foundation-learnings.md
```

The learning notes should capture:

- questions that came up during the milestone
- decisions made and why
- options considered and rejected
- beginner-friendly explanations in simple English
- concrete examples, sample values, and copy-pasteable commands when useful
- analogies that explain the concept without hiding the technical truth
- diagrams for flows, boundaries, or configuration precedence when they make the idea easier to remember
- production-minded tradeoffs
- mistakes or confusing tool behavior
- debugging steps and fixes
- what should be remembered for future milestones
- what should intentionally not be implemented yet

Prefer complete and thorough milestone notes over short notes when the topic is foundational or confusing. Length is acceptable when the detail helps a future reader understand the project without replaying the original conversation.

Simple rule:

```text
Code shows what changed.
Docs explain what changed, why it changed, how it works, and how to verify it.
```

For security, authentication, infrastructure, and workflow topics, include:

- what the setting or component is
- why the project needs it
- where it lives in the repository or local machine
- how values are saved and loaded
- what is safe to commit
- what must stay local or secret
- how to verify the setup
- common mistakes and how to recover

## Preferred Explanation Shape

When a concept is new, confusing, security-sensitive, or likely to be revisited later, use a rich explanation shape like this:

```text
Concept name:
  The short plain-English definition.

Why it exists:
  The practical problem it solves in this project.

Think of it like this:
  A concrete analogy that maps to the real behavior.

Where it lives:
  The exact file, folder, dashboard page, command, or local machine location.

How it works:
  The step-by-step flow in simple language.

Example:
  A small realistic sample value, request, command, or configuration snippet.

Verification:
  The command or manual check that proves it is working.

Common mistake:
  The easy thing to get wrong and how to fix it.
```

Example style:

```text
Project file:
  "My private local settings are stored under this ID."

Your computer:
  "I have a secrets.json file for that ID."
```

This style is especially useful in learning notes, runbooks, architecture explanations, security decisions, and milestone handoffs. It is acceptable for these docs to be lengthy when the length preserves useful learning context.

## Milestone Naming Convention

Use the same milestone title format everywhere:

```text
Milestone N - Title Case Name
```

Examples:

```text
Milestone 1 - Repository And Documentation Foundation
Milestone 2 - Backend Foundation
Milestone 3 - Name To Be Approved
```

Apply this format consistently in:

- `README.md`
- `CHANGELOG.md`
- `docs/project-status.md`
- milestone learning notes
- architecture decision records when they mention a milestone
- new milestone session/context window handoff prompts

Do not mix formats such as `milestone 3`, `M3`, `Phase 3`, or `Milestone 3: Name` unless the project intentionally changes the naming convention and documents that decision.

## Milestone Branch Convention

Start each new milestone on its own Git branch.

Branch names should use the milestone number and title in slug form:

```text
codex/milestone-N-title-case-name-as-kebab-case
```

Examples:

```text
codex/milestone-6-authentication-foundation
codex/milestone-7-identity-provider-integration
```

Create the new milestone branch from the latest committed closeout state of the previous milestone. That means the new branch includes all code, docs, tests, scripts, and project-status updates from the milestone that just finished.

Simple rule:

```text
Previous milestone closeout commit
  -> create next milestone branch
  -> update docs/project-status.md with the new branch and pending scope
  -> begin planning the new milestone
```

Do not continue new milestone implementation on an older milestone branch. If a milestone starts on the wrong branch, create the correct milestone branch from the latest closeout commit before doing more work.

## Why This Matters

The project is intended to be a learning and portfolio-quality system.

The code shows what was built. The learning notes explain why it was built that way.

That distinction matters. Future projects can reuse the reasoning, avoid repeated setup mistakes, and start closer to a production-ready shape.

## Rule For Changing The Plan

Plans can change.

When a later milestone proves an earlier decision is wrong, outdated, too early, or too complex, update the relevant docs instead of silently changing direction.

Record:

- what changed
- why it changed
- what the new direction is
- whether any previous learning note should be treated as superseded

This keeps the project honest and makes later decisions easier to defend.

## Milestone Closeout Checklist

Before committing a milestone:

0. Confirm the current branch matches the milestone number and title.
1. Build the solution.
2. Run the relevant tests.
3. Run whitespace/diff checks when available.
4. Update `CHANGELOG.md`.
5. Update `docs/project-status.md`.
6. Add or update the milestone learning notes.
7. Confirm the next milestone boundary is clear.
8. Commit only after the code and docs agree.
9. Create the next milestone session or context window with a clear handoff prompt.

The handoff prompt should include:

- workspace path
- current branch
- latest previous milestone closeout commit
- latest commit id
- required files to read first
- current milestone status
- next candidate milestone directions
- collaboration rules
- explicit reminder not to implement until the user approves the next milestone scope

This keeps each milestone session self-contained. The new session should be able to continue from project files and the handoff prompt without requiring the user to repaste the previous conversation.
