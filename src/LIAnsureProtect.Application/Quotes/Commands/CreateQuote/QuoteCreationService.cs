using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Application.Quotes.Rating;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public interface IQuoteCreationService
{
    IReadOnlyList<ControlAssertionDecision> EvaluateAssertions(CreateQuoteCommand request, Quote? existingQuote);

    Task<CreateQuoteResult> CreateAsync(
        Submission submission,
        Quote? existingQuote,
        CreateQuoteCommand request,
        IReadOnlyList<ControlAssertionDecision> assertionDecisions,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);
}

public sealed class QuoteCreationService(
    IQuoteRepository quoteRepository,
    ICyberRatingStrategySelector ratingStrategySelector,
    IRatingProviderClient ratingProviderClient) : IQuoteCreationService
{
    private static readonly JsonSerializerOptions PayloadHashJsonOptions = JsonSerializerOptions.Web;

    public IReadOnlyList<ControlAssertionDecision> EvaluateAssertions(CreateQuoteCommand request, Quote? existingQuote)
    {
        var decisions = ControlAssurancePolicy.Evaluate(new CreateQuoteAssuranceInput(
            request.RequestedLimit,
            request.MfaStatus,
            request.EdrStatus,
            request.BackupMaturity,
            request.HasIncidentResponsePlan,
            request.PriorCyberIncidents,
            request.SensitiveDataExposure,
            request.ControlDetails));

        return existingQuote is null
            ? decisions
            : ControlAssurancePolicy.ApplyReassessmentRules(decisions, existingQuote.ControlAssertions);
    }

    public async Task<CreateQuoteResult> CreateAsync(
        Submission submission,
        Quote? existingQuote,
        CreateQuoteCommand request,
        IReadOnlyList<ControlAssertionDecision> assertionDecisions,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
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
        existingQuote?.Supersede(createdAtUtc);
        var quote = Quote.Generate(
            submission.Id,
            submission.OwnerUserId,
            ratingResult.Premium,
            request.RequestedLimit,
            request.Retention,
            ratingResult.RiskTier,
            ratingResult.StrategyName,
            ratingResult.Subjectivities,
            ratingResult.ReferralReasons,
            createdAtUtc,
            version: existingQuote is null ? 1 : existingQuote.Version + 1,
            supersedesQuoteId: existingQuote?.Id,
            attestedByUserId: submission.OwnerUserId,
            attestedByName: request.AttestedByName,
            attestedByTitle: request.AttestedByTitle,
            attestationWordingVersion: ControlAssurancePolicy.AttestationWordingVersion,
            evidenceRequiredCount: assertionDecisions.Count(decision => decision.EvidenceRequired),
            submissionReference: submission.Reference,
            companyName: submission.CompanyName);

        foreach (var decision in assertionDecisions)
        {
            quote.AddControlAssertion(ControlAssertion.Create(
                quote.Id,
                quote.Version,
                decision.ControlType,
                decision.ClaimedState,
                decision.EvidenceRequired,
                decision.EvidenceReason,
                createdAtUtc,
                decision.DetailsJson));
        }

        var providerRequest = new RatingProviderRequest(
            quote.Id,
            submission.Id,
            submission.OwnerUserId,
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
        var providerResult = await ratingProviderClient.GetMarketIndicationAsync(providerRequest, cancellationToken);
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

        return ToResult(quote, RatingProviderIndicationResult.FromProviderResult(providerResult));
    }

    public static CreateQuoteResult ToResult(Quote quote, RatingProviderIndicationResult providerIndication)
        => new(
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
                    assertion.EvidenceReason,
                    assertion.DetailsJson))
                .ToList());

    public static RatingProviderIndicationResult CreateExistingQuoteProviderIndication(Quote quote)
        => new(
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

    private static List<string> SplitLines(string value)
        => value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string CreateRequestPayloadHash(RatingProviderRequest request)
    {
        var payload = JsonSerializer.Serialize(request, PayloadHashJsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
