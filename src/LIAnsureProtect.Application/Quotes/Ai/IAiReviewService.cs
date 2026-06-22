namespace LIAnsureProtect.Application.Quotes.Ai;

public interface IAiReviewService
{
    Task<AiReviewProviderResult> GenerateUnderwritingReviewAsync(
        AiReviewProviderRequest request,
        CancellationToken cancellationToken);
}
