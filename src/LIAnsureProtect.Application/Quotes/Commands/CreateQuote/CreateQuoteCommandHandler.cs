using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Application.Quotes.Rating;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Application.Quotes.Assurance;
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

        var existingQuote = await quoteRepository.GetLatestOwnedForSubmissionAsync(
            submission.Id,
            ownerUserId,
            cancellationToken);

        if (existingQuote is not null && !request.IsReassessment)
            return ToResult(existingQuote, CreateExistingQuoteProviderIndication(existingQuote));

        var assertionDecisions = ControlAssurancePolicy.Evaluate(new CreateQuoteAssuranceInput(
            request.RequestedLimit,
            request.MfaStatus,
            request.EdrStatus,
            request.BackupMaturity,
            request.HasIncidentResponsePlan,
            request.PriorCyberIncidents,
            request.SensitiveDataExposure,
            request.ControlDetails));
        if (existingQuote is not null)
        {
            assertionDecisions = ControlAssurancePolicy.ApplyReassessmentRules(
                assertionDecisions,
                existingQuote.ControlAssertions);
        }

        var evidenceRequiredCount = assertionDecisions.Count(decision => decision.EvidenceRequired);

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
            request.SensitiveDataExposure,
            request.OtherIndustryDescription,
            request.PriorCyberIncidentTypes,
            request.PriorCyberIncidentDetails);
        var ratingResult = ratingStrategySelector.Rate(ratingInput);
        var quoteCreatedAtUtc = DateTime.UtcNow;
        existingQuote?.Supersede(quoteCreatedAtUtc);
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
            quoteCreatedAtUtc,
            version: existingQuote is null ? 1 : existingQuote.Version + 1,
            supersedesQuoteId: existingQuote?.Id,
            attestedByUserId: ownerUserId,
            attestedByName: request.AttestedByName,
            attestedByTitle: request.AttestedByTitle,
            attestationWordingVersion: ControlAssurancePolicy.AttestationWordingVersion,
            evidenceRequiredCount: evidenceRequiredCount);

        foreach (var decision in assertionDecisions)
        {
            quote.AddControlAssertion(ControlAssertion.Create(
                quote.Id,
                quote.Version,
                decision.ControlType,
                decision.ClaimedState,
                decision.EvidenceRequired,
                decision.EvidenceReason,
                quoteCreatedAtUtc,
                decision.DetailsJson));
        }
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
            request.OtherIndustryDescription,
            request.PriorCyberIncidentTypes,
            request.PriorCyberIncidentDetails,
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
        await quoteRepository.AddRatingProviderAttemptAsync(providerAttempt, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResult(
            quote,
            RatingProviderIndicationResult.FromProviderResult(providerResult));
    }

    private static CreateQuoteResult ToResult(
        Quote quote,
        RatingProviderIndicationResult providerIndication)
    {
        return new CreateQuoteResult(
            quote.Id,
            quote.SubmissionId,
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.RiskTier.ToString(),
            quote.Status.ToString(),
            SplitLines(quote.Subjectivities),
            SplitLines(quote.ReferralReasons),
            quote.ExpiresAtUtc,
            providerIndication,
            quote.Version,
            quote.SupersedesQuoteId,
            quote.AssuranceStatus.ToString(),
            quote.EvidenceRequiredCount,
            quote.EvidenceSatisfiedCount,
            quote.ControlAssertions
                .OrderBy(assertion => assertion.ControlType)
                .Select(assertion => new ControlAssertionResult(
                    assertion.ControlType.ToString(),
                    assertion.ClaimedState,
                    assertion.AssuranceState.ToString(),
                    assertion.EvidenceRequired,
                    assertion.EvidenceReason))
                .ToList());
    }

    private static RatingProviderIndicationResult CreateExistingQuoteProviderIndication(Quote quote)
    {
        return new RatingProviderIndicationResult(
            "ExistingQuote",
            "AlreadyCreated",
            quote.Status.ToString(),
            null,
            null,
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            null,
            "None",
            null,
            0,
            0);
    }

    private static List<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
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
