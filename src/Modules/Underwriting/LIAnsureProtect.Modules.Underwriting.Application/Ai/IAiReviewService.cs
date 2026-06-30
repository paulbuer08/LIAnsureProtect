namespace LIAnsureProtect.Modules.Underwriting.Application.Ai;

/// <summary>
/// Outbound port to the advisory AI underwriting provider (local simulated now). Advisory only:
/// it returns a structured review packet and never makes an insurance decision.
/// </summary>
public interface IAiReviewService
{
    Task<AiReviewProviderResult> GenerateUnderwritingReviewAsync(
        AiReviewProviderRequest request,
        CancellationToken cancellationToken);
}
