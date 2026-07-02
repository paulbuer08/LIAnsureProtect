# Chapter 7 — Flow: Quoting & Rating

**Trigger:** the owner of a *submitted* submission requests a quote —
`POST /api/v1/submissions/{submissionId}/quotes` (`SubmissionQuotesController`, idempotent).
**Result:** a priced `quotes` row (status `Quoted` — or `Referred` for risky profiles, which
starts the underwriting flow of Chapter 8), an audited external-provider attempt, and a
`QuoteGeneratedDomainEvent` in the outbox.

> **Analogy:** pricing works like a **restaurant kitchen with two chefs**. The house chef
> (local rating strategies) always cooks the dish — deterministic and fast. A consultant chef
> (the external rating provider) is *asked for an opinion* and that opinion is filed, but if the
> consultant is sick or slow, the dish still goes out. Some dishes get flagged "the head chef
> must taste this first" — that's a referral.

## The flow, mirrored to the code

```mermaid
sequenceDiagram
    autonumber
    actor User as Owner (Customer/Broker)
    participant Ctrl as SubmissionQuotesController<br/>POST /api/v1/submissions/{id}/quotes
    participant H as CreateQuoteCommandHandler
    participant Sel as CyberRatingStrategySelector
    participant Strat as BaselineCyberRatingStrategy /<br/>HighRiskCyberRatingStrategy
    participant Dom as Quote.Generate(...)
    participant Prov as RatingProviderHttpClient<br/>(typed HttpClient + resilience)
    participant Ctx as SubmissionDbContext (via IUnitOfWork)

    User->>Ctrl: risk answers (MFA, EDR, backups, revenue, limit...)
    Note over Ctrl: [Authorize(Policy = Quotes.Create)] + Idempotency-Key
    Ctrl->>H: sender.Send(CreateQuoteCommand)
    H->>H: GetOwnedForUpdateAsync(submissionId, ownerUserId)<br/>null → 404; not Submitted → 409
    H->>Sel: Rate(CyberRatingInput)
    Sel->>Strat: picks a strategy by risk profile
    Strat-->>H: CyberRatingResult (premium, risk tier,<br/>subjectivities, referral reasons)
    H->>Dom: Quote.Generate(submissionId, owner, premium, tier, ...)
    Note over Dom: referral reasons present →<br/>status = Referred, else Quoted.<br/>Records QuoteGeneratedDomainEvent
    H->>Prov: GetMarketIndicationAsync(providerRequest)
    Note over Prov: IHttpClientFactory typed client with<br/>retry + circuit breaker + timeout.<br/>Failure → recorded, never fatal
    H->>H: QuoteRatingProviderAttempt.Record(...)<br/>(status, disposition, premium indication,<br/>failure category, duration, payload hash)
    H->>Ctx: AddAsync(quote) + AddRatingProviderAttemptAsync + SaveChangesAsync
    Note over Ctx: quote + provider attempt + outbox event —<br/>ONE transaction
    Ctrl-->>User: 201 with premium, tier, status,<br/>subjectivities, provider indication
```

## The pieces, explained

### 1. The rating engine (Strategy pattern)

`CyberRatingStrategySelector` inspects the `CyberRatingInput` (industry class, revenue band,
security controls like MFA/EDR/backups, incident history, data exposure) and picks:

- `BaselineCyberRatingStrategy` — standard risks: base premium from revenue/limit/retention with
  control-based credits/debits (`CyberRatingMath`).
- `HighRiskCyberRatingStrategy` — risky profiles (weak controls, prior incidents, sensitive
  data): higher factors **plus referral reasons** such as "prior incidents require underwriter
  review".

Adding a new risk profile = adding a strategy class; nothing else changes.

### 2. Quoted vs Referred — the fork in the road

`Quote.Generate(...)` sets status **`Quoted`** when there are no referral reasons, **`Referred`**
otherwise. Both record `QuoteGeneratedDomainEvent`, and the dispatcher (Chapter 10) fans it out:

- a **quote-ready notification** to the owner's inbox, and
- for `Referred` — a **referral operation projection** that makes the quote appear in the
  underwriter workbench queue (Chapter 8).

### 3. The external rating provider (resilient by construction)

`IRatingProviderClient` (Application port) → `RatingProviderHttpClient` (Infrastructure typed
client). Locally, `SimulatedRatingProviderHttpMessageHandler` plays the provider, and
`RatingProviderAttemptCountingHandler` counts real attempts (so retries are visible in the audit).

**Every** call — success, decline, timeout, malformed response — is recorded as a
`QuoteRatingProviderAttempt` row: provider name, disposition, indicated premium, HTTP status,
failure category, attempt count, duration, and a SHA-256 hash of the request payload (so we can
prove *what* we asked without storing the raw payload twice). The provider can **enrich** the
quote response with a market indication, but the local premium stands on its own — a provider
outage degrades the answer, never blocks it.

### 4. Reading quotes

The submission detail endpoint/page shows the quote(s) with premium, tier, subjectivities and
provider indication. Underwriters see referred quotes through their own queue (Chapter 8) —
never through the owner's endpoints.

## Scenario walk-through

> **Clean case:** Maria quotes a software firm with MFA + EDR + tested backups. Baseline strategy
> prices it, no referral reasons → `Quoted` instantly; the provider returns a supportive market
> indication; Maria's client gets a "quote ready" notification.
>
> **Risky case:** a hospital group with prior ransomware incidents and no EDR. High-risk strategy
> prices it with debits and referral reasons → `Referred`. Maria sees "referred to underwriting";
> underwriter Ben finds it in his workbench queue minutes later (Chapter 8). The provider call
> timed out that day — the attempt row records `Timeout`, and nothing else noticed.
