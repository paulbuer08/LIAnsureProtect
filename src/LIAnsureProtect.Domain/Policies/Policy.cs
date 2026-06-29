using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Domain.Policies;

public sealed class Policy : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];

    private Policy(
        Guid id,
        Guid quoteId,
        Guid submissionId,
        string ownerUserId,
        string policyNumber,
        decimal premium,
        decimal requestedLimit,
        decimal retention,
        DateTime effectiveDateUtc,
        DateTime expirationDateUtc,
        PolicyStatus status,
        string boundByUserId,
        DateTime boundAtUtc,
        DateTime createdAtUtc,
        string quoteStatusAtBind,
        string quoteRiskTierAtBind,
        string quoteSubjectivitiesAtBind)
    {
        Id = id;
        QuoteId = quoteId;
        SubmissionId = submissionId;
        OwnerUserId = ownerUserId;
        PolicyNumber = policyNumber;
        Premium = premium;
        RequestedLimit = requestedLimit;
        Retention = retention;
        EffectiveDateUtc = effectiveDateUtc;
        ExpirationDateUtc = expirationDateUtc;
        Status = status;
        BoundByUserId = boundByUserId;
        BoundAtUtc = boundAtUtc;
        CreatedAtUtc = createdAtUtc;
        QuoteStatusAtBind = quoteStatusAtBind;
        QuoteRiskTierAtBind = quoteRiskTierAtBind;
        QuoteSubjectivitiesAtBind = quoteSubjectivitiesAtBind;
    }

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

        var policy = new Policy(
            Guid.NewGuid(),
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            policyNumber.Trim(),
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            effectiveDateUtc,
            effectiveDateUtc.AddYears(1),
            PolicyStatus.Bound,
            boundByUserId.Trim(),
            boundAtUtc,
            boundAtUtc,
            quote.Status.ToString(),
            quote.RiskTier.ToString(),
            quote.Subjectivities);

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
