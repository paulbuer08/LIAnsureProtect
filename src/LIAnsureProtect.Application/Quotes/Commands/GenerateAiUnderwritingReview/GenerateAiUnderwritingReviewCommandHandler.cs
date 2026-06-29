using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Application.Quotes.Ai;
using LIAnsureProtect.Domain.Quotes;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.GenerateAiUnderwritingReview;

public sealed class GenerateAiUnderwritingReviewCommandHandler(
    IQuoteRepository quoteRepository,
    IAiReviewService aiReviewService,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<GenerateAiUnderwritingReviewCommand, GenerateAiUnderwritingReviewResult?>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GenerateAiUnderwritingReviewResult?> Handle(
        GenerateAiUnderwritingReviewCommand request,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return null;

        if (quote.Status != QuoteStatus.Referred)
            throw new InvalidOperationException("Only referred quotes can receive advisory AI underwriting review.");

        var requestedAtUtc = DateTime.UtcNow;
        var priorReviews = await quoteRepository.ListUnderwritingReviewsAsync(quote.Id, cancellationToken);
        var providerRequest = CreateProviderRequest(quote, priorReviews ?? [], requestedAtUtc);
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
                quote.Id,
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
                quote.Id,
                requestedByUserId,
                providerResult.ProviderName,
                AiReviewConstants.PromptVersion,
                AiReviewConstants.OutputSchemaVersion,
                inputSnapshotHash,
                providerResult.FailureReason ?? "AI review provider failed without a reason.",
                requestedAtUtc,
                providerResult.CompletedAtUtc);

        await quoteRepository.AddAiUnderwritingReviewAsync(review, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResult(review, quote);
    }

    private static AiReviewProviderRequest CreateProviderRequest(
        Quote quote,
        IReadOnlyCollection<QuoteUnderwritingReview> priorReviews,
        DateTime requestedAtUtc)
    {
        return new AiReviewProviderRequest(
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.RiskTier.ToString(),
            quote.Status.ToString(),
            quote.StrategyName,
            SplitLines(quote.Subjectivities),
            SplitLines(quote.ReferralReasons),
            priorReviews
                .OrderBy(review => review.CreatedAtUtc)
                .Select(review => $"{review.Decision}: {review.Reason}")
                .ToArray(),
            AiReviewConstants.PromptVersion,
            AiReviewConstants.OutputSchemaVersion,
            requestedAtUtc);
    }

    private static GenerateAiUnderwritingReviewResult ToResult(
        AiUnderwritingReview review,
        Quote quote)
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

    private static IReadOnlyCollection<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }
}
