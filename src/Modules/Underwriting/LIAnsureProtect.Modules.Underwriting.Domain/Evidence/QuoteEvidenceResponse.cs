using System.Security.Cryptography;

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
    public string EmailDomainStatus { get; private set; } = "Unverified";
    public string EmailVerificationStatus { get; private set; } = "Unverified";
    public string? EmailVerificationTokenHash { get; private set; }
    public DateTime? EmailVerificationExpiresAtUtc { get; private set; }
    public DateTime? EmailVerificationSentAtUtc { get; private set; }
    public DateTime? EmailVerifiedAtUtc { get; private set; }
    public int EmailVerificationSendCount { get; private set; }

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
        DateTime respondedAtUtc,
        string emailDomainStatus = "Unverified")
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
            respondedAtUtc,
            emailDomainStatus);
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
        DateTime respondedAtUtc,
        string emailDomainStatus = "Unverified")
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
            RespondedAtUtc = respondedAtUtc,
            EmailDomainStatus = string.IsNullOrWhiteSpace(emailDomainStatus) ? "Unverified" : emailDomainStatus.Trim(),
            EmailVerificationStatus = "Unverified"
        };
    }

    public void BeginEmailVerification(string tokenHash, DateTime sentAtUtc, DateTime expiresAtUtc)
    {
        if (EmailVerifiedAtUtc.HasValue)
            return;
        if (EmailVerificationSentAtUtc.HasValue && EmailVerificationSentAtUtc.Value > sentAtUtc.AddMinutes(-1))
            throw new InvalidOperationException("Wait one minute before requesting another verification email.");
        if (EmailVerificationSentAtUtc.HasValue && EmailVerificationSentAtUtc.Value <= sentAtUtc.AddHours(-24))
            EmailVerificationSendCount = 0;
        if (EmailVerificationSendCount >= 5
            && EmailVerificationSentAtUtc.HasValue
            && EmailVerificationSentAtUtc.Value > sentAtUtc.AddHours(-24))
            throw new InvalidOperationException("The daily verification-email limit is reached. Try again later.");

        EmailVerificationTokenHash = EvidenceResponseFieldRules.Required(
            tokenHash,
            nameof(tokenHash),
            "Email verification token hash",
            128);
        EmailVerificationSentAtUtc = sentAtUtc;
        EmailVerificationExpiresAtUtc = expiresAtUtc;
        EmailVerificationStatus = "VerificationPending";
        EmailVerificationSendCount++;
    }

    public void VerifyEmail(string tokenHash, DateTime verifiedAtUtc)
    {
        if (EmailVerifiedAtUtc.HasValue)
            throw new InvalidOperationException("This respondent email is already verified.");
        if (EmailVerificationTokenHash is null || !EmailVerificationExpiresAtUtc.HasValue)
            throw new InvalidOperationException("Request a verification email first.");
        if (EmailVerificationExpiresAtUtc.Value <= verifiedAtUtc)
            throw new InvalidOperationException("This verification code has expired. Request a new email.");
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(EmailVerificationTokenHash),
                Convert.FromHexString(tokenHash)))
            throw new InvalidOperationException("The verification code is invalid.");

        EmailVerifiedAtUtc = verifiedAtUtc;
        EmailVerificationStatus = "Verified";
        EmailVerificationTokenHash = null;
        EmailVerificationExpiresAtUtc = null;
    }

    public void RecordEmailVerificationDeliveryFailed()
    {
        if (EmailVerifiedAtUtc.HasValue)
            return;
        EmailVerificationStatus = "Unverified";
        EmailVerificationTokenHash = null;
        EmailVerificationExpiresAtUtc = null;
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
