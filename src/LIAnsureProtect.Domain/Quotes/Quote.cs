using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Quotes;

public sealed class Quote : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];

    private Quote(
        Guid id,
        Guid submissionId,
        string ownerUserId,
        decimal premium,
        decimal requestedLimit,
        decimal retention,
        CyberRiskTier riskTier,
        QuoteStatus status,
        string strategyName,
        string subjectivities,
        string referralReasons,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        SubmissionId = submissionId;
        OwnerUserId = ownerUserId;
        Premium = premium;
        RequestedLimit = requestedLimit;
        Retention = retention;
        RiskTier = riskTier;
        Status = status;
        StrategyName = strategyName;
        Subjectivities = subjectivities;
        ReferralReasons = referralReasons;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    private Quote()
    {
        OwnerUserId = string.Empty;
        StrategyName = string.Empty;
        Subjectivities = string.Empty;
        ReferralReasons = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SubmissionId { get; private set; }

    public string OwnerUserId { get; private set; }

    public decimal Premium { get; private set; }

    public decimal RequestedLimit { get; private set; }

    public decimal Retention { get; private set; }

    public CyberRiskTier RiskTier { get; private set; }

    public QuoteStatus Status { get; private set; }

    public string StrategyName { get; private set; }

    public string Subjectivities { get; private set; }

    public string ReferralReasons { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public static Quote Generate(
        Guid submissionId,
        string ownerUserId,
        decimal premium,
        decimal requestedLimit,
        decimal retention,
        CyberRiskTier riskTier,
        string strategyName,
        IReadOnlyCollection<string> subjectivities,
        IReadOnlyCollection<string> referralReasons,
        DateTime createdAtUtc)
    {
        if (submissionId == Guid.Empty)
            throw new ArgumentException("Submission id is required.", nameof(submissionId));

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));

        if (premium <= 0)
            throw new ArgumentOutOfRangeException(nameof(premium), "Premium must be greater than zero.");

        if (requestedLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedLimit), "Requested limit must be greater than zero.");

        if (retention <= 0)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be greater than zero.");

        var status = referralReasons.Count == 0
            ? QuoteStatus.Quoted
            : QuoteStatus.Referred;

        var quote = new Quote(
            Guid.NewGuid(),
            submissionId,
            ownerUserId,
            premium,
            requestedLimit,
            retention,
            riskTier,
            status,
            strategyName,
            JoinLines(subjectivities),
            JoinLines(referralReasons),
            createdAtUtc,
            createdAtUtc.AddDays(30));

        quote.domainEvents.Add(new QuoteGeneratedDomainEvent(
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.Status,
            createdAtUtc));

        return quote;
    }

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }

    private static string JoinLines(IReadOnlyCollection<string> values)
    {
        return string.Join("\n", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
