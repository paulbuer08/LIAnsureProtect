using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Rating;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using MediatR;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteCommandHandler(
    ISubmissionRepository submissionRepository,
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICyberRatingStrategySelector ratingStrategySelector,
    IRatingProviderClient ratingProviderClient)
    : IRequestHandler<CreateQuoteCommand, CreateQuoteResult?>
{
    private static readonly JsonSerializerOptions PayloadHashJsonOptions = JsonSerializerOptions.Web;

    public async Task<CreateQuoteResult?> Handle(
        CreateQuoteCommand request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = GetRequiredCurrentUserId();
        var submission = await submissionRepository.GetOwnedForUpdateAsync(
            request.SubmissionId,
            ownerUserId,
            cancellationToken);

        if (submission is null)
            return null;

        if (submission.Status != SubmissionStatus.Submitted)
            throw new InvalidOperationException("Only submitted submissions can be quoted.");

        var ratingInput = new CyberRatingInput(
            request.IndustryClass,
            request.AnnualRevenueBand,
            request.RequestedLimit,
            request.Retention,
            request.MfaStatus,
            request.EdrStatus,
            request.BackupMaturity,
            request.HasIncidentResponsePlan,
            request.PriorCyberIncidents,
            request.SensitiveDataExposure);
        var ratingResult = ratingStrategySelector.Rate(ratingInput);
        var quote = Quote.Generate(
            submission.Id,
            ownerUserId,
            ratingResult.Premium,
            request.RequestedLimit,
            request.Retention,
            ratingResult.RiskTier,
            ratingResult.StrategyName,
            ratingResult.Subjectivities,
            ratingResult.ReferralReasons,
            DateTime.UtcNow);
        var providerRequest = new RatingProviderRequest(
            quote.Id,
            submission.Id,
            ownerUserId,
            request.IndustryClass,
            request.AnnualRevenueBand,
            request.RequestedLimit,
            request.Retention,
            request.MfaStatus,
            request.EdrStatus,
            request.BackupMaturity,
            request.HasIncidentResponsePlan,
            request.PriorCyberIncidents,
            request.SensitiveDataExposure,
            quote.Premium,
            quote.RiskTier,
            quote.Status,
            ratingResult.StrategyName);
        var providerAttemptCreatedAtUtc = DateTime.UtcNow;
        var providerResult = await ratingProviderClient.GetMarketIndicationAsync(
            providerRequest,
            cancellationToken);
        var providerAttempt = QuoteRatingProviderAttempt.Record(
            quote.Id,
            providerResult.ProviderName,
            providerResult.Status,
            providerResult.MarketDisposition,
            providerResult.ProviderReference,
            providerResult.ProviderQuoteNumber,
            providerResult.IndicatedPremium,
            providerResult.IndicatedLimit,
            providerResult.IndicatedRetention,
            providerResult.HttpStatusCode,
            providerResult.FailureCategory,
            providerResult.FailureReason,
            providerResult.AttemptCount,
            providerResult.Duration,
            CreateRequestPayloadHash(providerRequest),
            providerAttemptCreatedAtUtc,
            providerResult.CompletedAtUtc);

        await quoteRepository.AddAsync(quote, cancellationToken);
        if (quote.Status == QuoteStatus.Referred)
        {
            var operation = QuoteReferralOperation.CreateDefault(
                quote.Id,
                quote.RiskTier,
                quote.CreatedAtUtc,
                quote.ExpiresAtUtc);
            await quoteRepository.AddReferralOperationAsync(operation, cancellationToken);
        }

        await quoteRepository.AddRatingProviderAttemptAsync(providerAttempt, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateQuoteResult(
            quote.Id,
            quote.SubmissionId,
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.RiskTier.ToString(),
            quote.Status.ToString(),
            ratingResult.Subjectivities,
            ratingResult.ReferralReasons,
            quote.ExpiresAtUtc,
            RatingProviderIndicationResult.FromProviderResult(providerResult));
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to create a quote.")
            : currentUser.UserId;
    }

    private static string CreateRequestPayloadHash(RatingProviderRequest request)
    {
        var payload = JsonSerializer.Serialize(request, PayloadHashJsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash);
    }
}
