using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Commands.GenerateAiUnderwritingReview;

public sealed record GenerateAiUnderwritingReviewCommand(Guid QuoteId)
    : IRequest<GenerateAiUnderwritingReviewResult?>;
