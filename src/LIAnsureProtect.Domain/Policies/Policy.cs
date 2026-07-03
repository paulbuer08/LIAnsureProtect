using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Domain.Policies;

public sealed class Policy : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];

    // The only constructor: EF Core materializes through it, and the BindFromAcceptedQuote factory
    // assigns state via the private property setters — no parameter-heavy constructor to maintain.
    private Policy()
    {
        OwnerUserId = string.Empty;
        PolicyNumber = string.Empty;
        BoundByUserId = string.Empty;
        QuoteStatusAtBind = string.Empty;
        QuoteRiskTierAtBind = string.Empty;
        QuoteSubjectivitiesAtBind = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public Guid SubmissionId { get; private set; }

    public string OwnerUserId { get; private set; }

    public string PolicyNumber { get; private set; }

    public decimal Premium { get; private set; }

    public decimal RequestedLimit { get; private set; }

    public decimal Retention { get; private set; }

    public DateTime EffectiveDateUtc { get; private set; }

    public DateTime ExpirationDateUtc { get; private set; }

    public PolicyStatus Status { get; private set; }

    public string BoundByUserId { get; private set; }

    public DateTime BoundAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string QuoteStatusAtBind { get; private set; }

    public string QuoteRiskTierAtBind { get; private set; }

    public string QuoteSubjectivitiesAtBind { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public static Policy BindFromAcceptedQuote(
        Quote quote,
        string policyNumber,
        string boundByUserId,
        DateTime effectiveDateUtc,
        DateTime boundAtUtc)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (quote.Status != QuoteStatus.Accepted)
            throw new InvalidOperationException("Only accepted quotes can be bound.");

        if (string.IsNullOrWhiteSpace(policyNumber))
            throw new ArgumentException("Policy number is required.", nameof(policyNumber));

        if (string.IsNullOrWhiteSpace(boundByUserId))
            throw new ArgumentException("Bound by user id is required.", nameof(boundByUserId));

        if (effectiveDateUtc == default)
            throw new ArgumentException("Effective date is required.", nameof(effectiveDateUtc));

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            QuoteId = quote.Id,
            SubmissionId = quote.SubmissionId,
            OwnerUserId = quote.OwnerUserId,
            PolicyNumber = policyNumber.Trim(),
            Premium = quote.Premium,
            RequestedLimit = quote.RequestedLimit,
            Retention = quote.Retention,
            EffectiveDateUtc = effectiveDateUtc,
            ExpirationDateUtc = effectiveDateUtc.AddYears(1),
            Status = PolicyStatus.Bound,
            BoundByUserId = boundByUserId.Trim(),
            BoundAtUtc = boundAtUtc,
            CreatedAtUtc = boundAtUtc,
            QuoteStatusAtBind = quote.Status.ToString(),
            QuoteRiskTierAtBind = quote.RiskTier.ToString(),
            QuoteSubjectivitiesAtBind = quote.Subjectivities
        };

        policy.domainEvents.Add(new PolicyBoundDomainEvent(
            policy.Id,
            policy.PolicyNumber,
            policy.QuoteId,
            policy.SubmissionId,
            policy.OwnerUserId,
            policy.BoundByUserId,
            boundAtUtc));

        return policy;
    }

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }
}
