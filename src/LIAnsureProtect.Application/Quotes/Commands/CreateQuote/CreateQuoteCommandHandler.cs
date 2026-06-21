using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Rating;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteCommandHandler(
    ISubmissionRepository submissionRepository,
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICyberRatingStrategySelector ratingStrategySelector)
    : IRequestHandler<CreateQuoteCommand, CreateQuoteResult?>
{
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

        await quoteRepository.AddAsync(quote, cancellationToken);
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
            quote.ExpiresAtUtc);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to create a quote.")
            : currentUser.UserId;
    }
}
