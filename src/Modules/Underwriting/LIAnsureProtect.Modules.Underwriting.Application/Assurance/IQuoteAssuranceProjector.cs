namespace LIAnsureProtect.Modules.Underwriting.Application.Assurance;

public interface IQuoteAssuranceProjector
{
    Task ProjectAsync(QuoteAssuranceEvent assuranceEvent, CancellationToken cancellationToken);
}

public sealed record QuoteAssuranceEvent(
    Guid SourceOutboxMessageId,
    Guid QuoteId,
    DateTime OccurredAtUtc);
