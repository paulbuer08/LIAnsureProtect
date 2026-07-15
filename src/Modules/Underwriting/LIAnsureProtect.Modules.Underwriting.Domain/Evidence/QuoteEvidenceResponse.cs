namespace LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

/// <summary>
/// Immutable audit entry for one customer/broker response. The parent request retains a latest-state
/// snapshot for queue compatibility, while this record preserves every submitted version.
/// </summary>
public sealed class QuoteEvidenceResponse
{
    private QuoteEvidenceResponse()
    {
    }

    public Guid Id { get; private set; }
    public Guid EvidenceRequestId { get; private set; }
    public Guid QuoteId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public string RespondedByUserId { get; private set; } = string.Empty;
    public string RespondentName { get; private set; } = string.Empty;
    public string RespondentTitle { get; private set; } = string.Empty;
    public string RespondentEmail { get; private set; } = string.Empty;
    // Retained so historic response rows created before contact fields were split remain readable.
    public string? RespondentPhone { get; private set; }
    public string? RespondentMobileNumber { get; private set; }
    public string? RespondentTelephoneNumber { get; private set; }
    public string? ResponseText { get; private set; }
    public string? OtherConcerns { get; private set; }
    public EvidenceResponseKind Kind { get; private set; }
    public DateTime RespondedAtUtc { get; private set; }
    public string? ViewedByUserId { get; private set; }
    public DateTime? ViewedAtUtc { get; private set; }

    public static QuoteEvidenceResponse Create(
        QuoteEvidenceRequest request,
        string respondedByUserId,
        string respondentName,
        string respondentTitle,
        string respondentEmail,
        string? respondentPhone,
        string? responseText,
        string? otherConcerns,
        EvidenceResponseKind kind,
        DateTime respondedAtUtc)
    {
        return Create(
            request,
            respondedByUserId,
            respondentName,
            respondentTitle,
            respondentEmail,
            respondentPhone,
            null,
            responseText,
            otherConcerns,
            kind,
            respondedAtUtc);
    }

    public static QuoteEvidenceResponse Create(
        QuoteEvidenceRequest request,
        string respondedByUserId,
        string respondentName,
        string respondentTitle,
        string respondentEmail,
        string? respondentMobileNumber,
        string? respondentTelephoneNumber,
        string? responseText,
        string? otherConcerns,
        EvidenceResponseKind kind,
        DateTime respondedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedResponse = EvidenceResponseFieldRules.Optional(
            responseText,
            nameof(responseText),
            "Evidence response",
            EvidenceResponseFieldRules.ResponseTextMaxLength);
        if (kind is EvidenceResponseKind.Initial or EvidenceResponseKind.Remediation
            && normalizedResponse is null)
        {
            throw new ArgumentException("Evidence response text is required.", nameof(responseText));
        }

        return new QuoteEvidenceResponse
        {
            Id = Guid.NewGuid(),
            EvidenceRequestId = request.Id,
            QuoteId = request.QuoteId,
            SubmissionId = request.SubmissionId,
            OwnerUserId = EvidenceResponseFieldRules.Required(request.OwnerUserId, nameof(request.OwnerUserId), "Owner user id", 256),
            RespondedByUserId = EvidenceResponseFieldRules.Required(respondedByUserId, nameof(respondedByUserId), "Responded-by user id", 256),
            RespondentName = EvidenceResponseFieldRules.Required(respondentName, nameof(respondentName), "Respondent name", EvidenceResponseFieldRules.RespondentNameMaxLength),
            RespondentTitle = EvidenceResponseFieldRules.Required(respondentTitle, nameof(respondentTitle), "Respondent title", EvidenceResponseFieldRules.RespondentTitleMaxLength),
            RespondentEmail = EvidenceResponseFieldRules.Email(respondentEmail),
            RespondentMobileNumber = EvidenceResponseFieldRules.PhilippineMobileNumber(respondentMobileNumber),
            RespondentTelephoneNumber = EvidenceResponseFieldRules.PhilippineTelephoneNumber(respondentTelephoneNumber),
            ResponseText = normalizedResponse,
            OtherConcerns = EvidenceResponseFieldRules.Optional(
                otherConcerns,
                nameof(otherConcerns),
                "Other concerns",
                EvidenceResponseFieldRules.OtherConcernsMaxLength),
            Kind = kind,
            RespondedAtUtc = respondedAtUtc
        };
    }

    public bool MarkViewed(string viewedByUserId, DateTime viewedAtUtc)
    {
        if (Kind != EvidenceResponseKind.FollowUp)
            throw new InvalidOperationException("Only customer follow-up responses use the unread workflow.");

        if (ViewedAtUtc.HasValue)
            return false;

        ViewedByUserId = EvidenceResponseFieldRules.Required(
            viewedByUserId,
            nameof(viewedByUserId),
            "Viewed-by user id",
            256);
        ViewedAtUtc = viewedAtUtc;
        return true;
    }
}
