namespace LIAnsureProtect.Application.Quotes.Assurance;

public interface IQuoteAssuranceDecisionProjector
{
    Task ProjectAsync(QuoteAssuranceDecisionEvent decisionEvent, CancellationToken cancellationToken);
}

public sealed record QuoteAssuranceDecisionEvent(
    Guid SourceOutboxMessageId,
    Guid QuoteId,
    string EvidenceCategory,
    bool Satisfied,
    string ReviewedByUserId,
    DateTime OccurredAtUtc);
