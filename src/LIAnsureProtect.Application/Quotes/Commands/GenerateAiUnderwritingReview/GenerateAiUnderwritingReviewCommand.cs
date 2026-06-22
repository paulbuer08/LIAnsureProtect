using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.GenerateAiUnderwritingReview;

public sealed record GenerateAiUnderwritingReviewCommand(Guid QuoteId)
    : IRequest<GenerateAiUnderwritingReviewResult?>;
