using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Assurance;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class QuoteAssuranceProjector(
    UnderwritingDbContext dbContext,
    IUnderwritingQuoteContextReader quoteContextReader,
    IEvidenceRequestRepository evidenceRequests) : IQuoteAssuranceProjector
{
    private const string SystemRequestedByUserId = "system-assurance-policy";

    public async Task ProjectAsync(QuoteAssuranceEvent assuranceEvent, CancellationToken cancellationToken)
    {
        if (await dbContext.QuoteAssuranceProjectedMessages.AnyAsync(
                message => message.SourceOutboxMessageId == assuranceEvent.SourceOutboxMessageId,
                cancellationToken))
        {
            return;
        }

        var quote = await quoteContextReader.GetForAssuranceAsync(assuranceEvent.QuoteId, cancellationToken);
        if (quote is null)
            return;

        foreach (var requirement in quote.Requirements.Where(requirement => requirement.EvidenceRequired))
        {
            if (await evidenceRequests.ExistsForQuoteCategoryAsync(
                    quote.QuoteId,
                    requirement.Category,
                    cancellationToken))
            {
                continue;
            }

            var category = ParseCategory(requirement.Category);
            var evidenceRequest = QuoteEvidenceRequest.Create(
                quote.QuoteId,
                quote.SubmissionId,
                quote.OwnerUserId,
                SystemRequestedByUserId,
                category,
                TitleFor(category),
                $"{requirement.Reason} Upload current supporting evidence for underwriting review. " +
                "Automated screening is advisory; an underwriter records the final evidence decision.",
                assuranceEvent.OccurredAtUtc.AddDays(14),
                assuranceEvent.OccurredAtUtc);

            await evidenceRequests.AddAsync(evidenceRequest, cancellationToken);
        }

        dbContext.QuoteAssuranceProjectedMessages.Add(
            QuoteAssuranceProjectedMessage.Record(assuranceEvent.SourceOutboxMessageId, DateTime.UtcNow));
        await evidenceRequests.SaveChangesAsync(cancellationToken);
    }

    private static EvidenceRequestCategory ParseCategory(string category)
    {
        return Enum.TryParse<EvidenceRequestCategory>(category, out var parsed)
            ? parsed
            : EvidenceRequestCategory.SecurityQuestionnaireClarification;
    }

    private static string TitleFor(EvidenceRequestCategory category) => category switch
    {
        EvidenceRequestCategory.MultiFactorAuthentication => "Verify multi-factor authentication",
        EvidenceRequestCategory.EndpointDetectionAndResponse => "Verify endpoint detection and response",
        EvidenceRequestCategory.BackupRecovery => "Verify backup and recovery controls",
        EvidenceRequestCategory.IncidentResponsePlan => "Verify incident response readiness",
        _ => "Clarify security control assertion"
    };
}
