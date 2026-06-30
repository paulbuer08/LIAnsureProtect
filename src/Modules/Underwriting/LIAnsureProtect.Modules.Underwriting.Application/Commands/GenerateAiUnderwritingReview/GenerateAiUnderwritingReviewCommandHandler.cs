using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Modules.Underwriting.Application.Ai;
using LIAnsureProtect.Modules.Underwriting.Domain;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Commands.GenerateAiUnderwritingReview;

public sealed class GenerateAiUnderwritingReviewCommandHandler(
    IUnderwritingQuoteContextReader quoteContextReader,
    IAiReviewService aiReviewService,
    IAiUnderwritingReviewRepository reviewRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GenerateAiUnderwritingReviewCommand, GenerateAiUnderwritingReviewResult?>
{
    // The quote status that may receive advisory AI review. A string here is the cross-context contract;
    // the module cannot reference the Quoting context's QuoteStatus enum.
    private const string ReferredStatus = "Referred";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GenerateAiUnderwritingReviewResult?> Handle(
        GenerateAiUnderwritingReviewCommand request,
        CancellationToken cancellationToken)
    {
        var quote = await quoteContextReader.GetForAiReviewAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return null;

        if (!string.Equals(quote.Status, ReferredStatus, StringComparison.Ordinal))
            throw new InvalidOperationException("Only referred quotes can receive advisory AI underwriting review.");

        var requestedAtUtc = DateTime.UtcNow;
        var providerRequest = CreateProviderRequest(quote, requestedAtUtc);
        var inputSnapshotHash = ComputeInputSnapshotHash(providerRequest);
        var requestedByUserId = GetRequiredCurrentUserId();

        AiReviewProviderResult providerResult;
        try
        {
            providerResult = await aiReviewService.GenerateUnderwritingReviewAsync(providerRequest, cancellationToken);
        }
        catch (Exception exception)
        {
            providerResult = AiReviewProviderResult.Failed(
                "Unknown AI Provider",
                exception.Message,
                DateTime.UtcNow);
        }

        var review = providerResult.IsSuccessful
            ? AiUnderwritingReview.Succeeded(
                quote.QuoteId,
                requestedByUserId,
                providerResult.ProviderName,
                AiReviewConstants.PromptVersion,
                AiReviewConstants.OutputSchemaVersion,
                inputSnapshotHash,
                providerResult.ExecutiveSummary ?? string.Empty,
                Serialize(providerResult.PositiveRiskSignals),
                Serialize(providerResult.NegativeRiskSignals),
                Serialize(providerResult.ControlGaps),
                Serialize(providerResult.SuggestedUnderwritingQuestions),
                Serialize(providerResult.SuggestedSubjectivityCandidates),
                Serialize(providerResult.Citations),
                Serialize(providerResult.Limitations),
                providerResult.AdvisoryDisclaimer ?? AiReviewConstants.AdvisoryDisclaimer,
                requestedAtUtc,
                providerResult.CompletedAtUtc)
            : AiUnderwritingReview.Failed(
                quote.QuoteId,
                requestedByUserId,
                providerResult.ProviderName,
                AiReviewConstants.PromptVersion,
                AiReviewConstants.OutputSchemaVersion,
                inputSnapshotHash,
                providerResult.FailureReason ?? "AI review provider failed without a reason.",
                requestedAtUtc,
                providerResult.CompletedAtUtc);

        await reviewRepository.AddAsync(review, cancellationToken);

        return ToResult(review, quote);
    }

    private static AiReviewProviderRequest CreateProviderRequest(
        UnderwritingQuoteContext quote,
        DateTime requestedAtUtc)
    {
        return new AiReviewProviderRequest(
            quote.QuoteId,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.RiskTier,
            quote.Status,
            quote.StrategyName,
            quote.Subjectivities,
            quote.ReferralReasons,
            quote.PriorUnderwritingDecisions,
            AiReviewConstants.PromptVersion,
            AiReviewConstants.OutputSchemaVersion,
            requestedAtUtc);
    }

    private static GenerateAiUnderwritingReviewResult ToResult(
        AiUnderwritingReview review,
        UnderwritingQuoteContext quote)
    {
        return new GenerateAiUnderwritingReviewResult(
            review.Id,
            review.QuoteId,
            quote.SubmissionId,
            review.Status.ToString(),
            review.ProviderName,
            review.PromptVersion,
            review.OutputSchemaVersion,
            review.InputSnapshotHash,
            review.ExecutiveSummary,
            Deserialize(review.PositiveRiskSignals),
            Deserialize(review.NegativeRiskSignals),
            Deserialize(review.ControlGaps),
            Deserialize(review.SuggestedUnderwritingQuestions),
            Deserialize(review.SuggestedSubjectivityCandidates),
            Deserialize(review.Citations),
            Deserialize(review.Limitations),
            review.AdvisoryDisclaimer,
            review.FailureReason,
            review.CreatedAtUtc,
            review.CompletedAtUtc);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated underwriter user id is required to request AI underwriting review.")
            : currentUser.UserId;
    }

    private static string ComputeInputSnapshotHash(AiReviewProviderRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Serialize(IReadOnlyCollection<string> values)
    {
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static IReadOnlyCollection<string> Deserialize(string json)
    {
        return JsonSerializer.Deserialize<IReadOnlyCollection<string>>(json, JsonOptions) ?? [];
    }
}
