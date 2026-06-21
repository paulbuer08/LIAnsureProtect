using MediatR;

namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

public sealed record ListQuoteReferralsQuery : IRequest<ListQuoteReferralsResult>;
