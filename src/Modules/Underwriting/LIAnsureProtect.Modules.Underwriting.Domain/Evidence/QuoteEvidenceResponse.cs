using System.Net.Mail;

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
    public string? RespondentPhone { get; private set; }
    public string? ResponseText { get; private set; }
    public string? OtherConcerns { get; private set; }
    public EvidenceResponseKind Kind { get; private set; }
    public DateTime RespondedAtUtc { get; private set; }

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
        ArgumentNullException.ThrowIfNull(request);

        var normalizedResponse = NormalizeOptional(responseText);
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
            OwnerUserId = ValidateRequired(request.OwnerUserId, nameof(request.OwnerUserId)),
            RespondedByUserId = ValidateRequired(respondedByUserId, nameof(respondedByUserId)),
            RespondentName = ValidateRequired(respondentName, nameof(respondentName)),
            RespondentTitle = ValidateRequired(respondentTitle, nameof(respondentTitle)),
            RespondentEmail = ValidateEmail(respondentEmail),
            RespondentPhone = NormalizeOptional(respondentPhone),
            ResponseText = normalizedResponse,
            OtherConcerns = NormalizeOptional(otherConcerns),
            Kind = kind,
            RespondedAtUtc = respondedAtUtc
        };
    }

    private static string ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);

        return value.Trim();
    }

    private static string ValidateEmail(string value)
    {
        var trimmed = ValidateRequired(value, nameof(value));
        if (!MailAddress.TryCreate(trimmed, out var address)
            || !string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Respondent email must be a valid email address.", nameof(value));
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
