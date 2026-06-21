namespace LIAnsureProtect.Application.Quotes.Commands.AcceptQuote;

public sealed record AcceptQuoteResult(
    Guid QuoteId,
    Guid SubmissionId,
    string Status,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string Subjectivities,
    DateTime ExpiresAtUtc,
    string AcceptedByUserId,
    string AcceptedByName,
    string AcceptedByTitle,
    bool SubjectivitiesAcknowledged,
    DateTime AcceptedAtUtc);
