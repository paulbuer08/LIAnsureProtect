using MediatR;

namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

public sealed record AdjustQuoteReferralCommand(
    Guid QuoteId,
    decimal AdjustedPremium,
    decimal AdjustedRetention,
    string Reason,
    string? Notes,
    string? UpdatedSubjectivities) : IRequest<UnderwriteQuoteReferralResult?>;
