# Milestone 35 - Underwriting Module: AI Review (first Underwriting slice) — Learning Notes

## Goal

Begin carving the **Underwriting** context — the hardest in the program because it is entangled with
the `Quote` aggregate. M35 takes the first, safest slice: lift the **advisory AI underwriting review**
into a new `src/Modules/Underwriting/{Domain,Application,Infrastructure}` with its own `underwriting`
PostgreSQL schema, and stand up the **cross-context Quote-read contract** that later slices reuse.
Behavior-preserving: `POST /api/v1/underwriting/quote-referrals/{quoteId}/ai-review` returns the same
result; the frontend is untouched.

## Why AI review was the right first slice

The underwriting *decision* (`Quote.ApproveReferral/DeclineReferral/AdjustReferral`) lives **on the
Quote aggregate** and mutates quote state — it can't move without moving Quoting. AI review, by
contrast, only **reads** quote context and writes its own audit table; it never mutates the quote. So
it carves cleanly and lets us prove the read-contract pattern with zero risk to quote state.

## The cross-context read contract

The module can't reference the `Quote` aggregate (that's the Quoting context). So it reads a snapshot
through a port it owns:

```
module Application:  IUnderwritingQuoteContextReader.GetForAiReviewAsync(quoteId) → UnderwritingQuoteContext?
legacy Infrastructure: QuoteUnderwritingContextReader implements it (reads Quote + prior
                       QuoteUnderwritingReview via SubmissionDbContext) and maps to the snapshot record.
```

`UnderwritingQuoteContext` is a plain record (premium, limit, retention, risk tier, status, strategy,
subjectivities, referral reasons, prior decisions). The handler guards `Status == "Referred"` (a string
— the module can't see the `QuoteStatus` enum) and never touches the quote. This is the same
"legacy implements a module port" seam M33 introduced for the dispatcher → projector, but here the
direction is a **read** (module pulls quote context) rather than a write.

## Structural guardrail (stronger than before)

Previously the "AI cannot make an insurance decision" guarantee was enforced by a unit test asserting
the handler didn't call quote-mutating methods. Now it is **structural**: the Underwriting module has
no project reference to `Quote` at all, so it physically cannot approve/decline/adjust/accept/bind. The
unit test now just verifies it reads a snapshot and persists an `AiUnderwritingReview`.

## Dropping the cross-aggregate FK

The legacy `AiUnderwritingReview` had a `Quote` navigation + a foreign key to `quotes`. A modular
monolith forbids cross-schema/cross-module FKs (reference by id + events only), so the moved entity
drops the navigation and the config drops the `HasOne(...Quote)` relationship — `QuoteId` is now just
a column.

## Schema move + third DbContext

`ai_underwriting_reviews` moved public → `underwriting` by drop-and-recreate (`SubmissionDbContext`
migration `DropAiUnderwritingReviews`; `UnderwritingDbContext` migration `CreateUnderwritingSchema`),
since there is no production data. There are now **three** DbContexts (Submission, Notifications,
Underwriting); the dev scripts, the migrations guard in `common.ps1`, and `ci.yml` apply each with
`--context`. The pattern was already established in M33, so adding the third context was mechanical.

## A subtlety: the AI provider stays behind the profile switch

`LocalSimulatedAiReviewService` moved into the module and is registered behind `Platform:Profile`
(Local now; `Aws` fails fast — a future Bedrock adapter). `AddInfrastructure` no longer registers it;
`AddUnderwritingModule` does. The legacy reads (`GetForUnderwritingReviewAsync`,
`ListUnderwritingReviewsAsync`) stay in `IQuoteRepository` because the context-reader adapter reuses
them; only `AddAiUnderwritingReviewAsync` left the repository.

## Verification

- `dotnet build LIAnsureProtect.slnx` — 0/0.
- `dotnet test` — UnitTests 62, IntegrationTests 114 (+1 PostgreSQL opt-in skipped): the moved guardrail
  unit tests, the reworked three-context AI endpoint test, the AI service test, the new
  `UnderwritingDbContext` migration test, and service resolution.
- `dotnet ef migrations has-pending-model-changes` clean for **all three** contexts.
- Frontend `features/underwriting` unchanged; full local CI applies three contexts' migrations.

## What's next (the rest of the Underwriting carve)

- **Referral operations** (queue, assignment, notes, tasks, timeline) — its own slice; created when a
  quote is referred, so it needs a Quoting → Underwriting hand-off (event or port).
- **Evidence** (requests, documents, reviews) — the largest sub-context.
- **The decision** (`ApproveReferral/DeclineReferral/AdjustReferral` + `QuoteUnderwritingReview`) — the
  hardest; it likely stays with the Quote aggregate or moves via a Quoting command port, and is where
  `IUnitOfWork` finally relocates into `Platform.Abstractions`.
