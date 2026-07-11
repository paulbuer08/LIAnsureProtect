using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Quotes;

public sealed class QuoteAssuranceDecisionProjector(SubmissionDbContext dbContext)
    : IQuoteAssuranceDecisionProjector
{
    public async Task ProjectAsync(
        QuoteAssuranceDecisionEvent decisionEvent,
        CancellationToken cancellationToken)
    {
        if (await dbContext.QuoteAssuranceDecisionProjectedMessages.AnyAsync(
                message => message.SourceOutboxMessageId == decisionEvent.SourceOutboxMessageId,
                cancellationToken))
        {
            return;
        }

        var quote = await dbContext.Quotes
            .Include(item => item.ControlAssertions)
            .SingleOrDefaultAsync(item => item.Id == decisionEvent.QuoteId, cancellationToken);
        if (quote is null)
            return;

        var controlType = MapControlType(decisionEvent.EvidenceCategory);
        if (controlType is not null)
        {
            quote.RecordAssuranceDecision(
                controlType.Value,
                decisionEvent.Satisfied,
                decisionEvent.ReviewedByUserId,
                decisionEvent.OccurredAtUtc);
        }

        dbContext.QuoteAssuranceDecisionProjectedMessages.Add(
            QuoteAssuranceDecisionProjectedMessage.Record(
                decisionEvent.SourceOutboxMessageId,
                DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ControlType? MapControlType(string category) => category switch
    {
        "MultiFactorAuthentication" => ControlType.MultiFactorAuthentication,
        "EndpointDetectionAndResponse" => ControlType.EndpointDetectionAndResponse,
        "BackupRecovery" => ControlType.BackupRecovery,
        "IncidentResponsePlan" => ControlType.IncidentResponsePlan,
        "SecurityQuestionnaireClarification" => ControlType.SensitiveData,
        _ => null
    };
}
