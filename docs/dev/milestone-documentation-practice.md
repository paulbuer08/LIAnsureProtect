# Milestone Documentation Practice

This project treats documentation as part of the implementation, not as an afterthought.

Each milestone should leave enough written context that a future session can continue without needing to replay the entire conversation.

## Required Documents To Update

For every meaningful milestone, update:

- `README.md` when the user-facing project status or documentation index changes.
- `CHANGELOG.md` when files, architecture, tests, or behavior change.
- `docs/project-status.md` for the current branch, milestone status, verification, next step, and decisions to remember.
- Relevant files under `docs/architecture/` when architecture decisions change.
- Relevant files under `docs/dev/` when setup, tooling, verification, or workflow changes.

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
- beginner-friendly explanations
- production-minded tradeoffs
- mistakes or confusing tool behavior
- debugging steps and fixes
- what should be remembered for future milestones
- what should intentionally not be implemented yet

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
- latest commit id
- required files to read first
- current milestone status
- next candidate milestone directions
- collaboration rules
- explicit reminder not to implement until the user approves the next milestone scope

This keeps each milestone session self-contained. The new session should be able to continue from project files and the handoff prompt without requiring the user to repaste the previous conversation.
