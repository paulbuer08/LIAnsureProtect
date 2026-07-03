namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// An adjuster's request for more information from the claimant, and the claimant's answer.
/// Created and answered only through the <see cref="Claim"/> aggregate, which owns the status
/// flips (UnderReview → InformationRequested → UnderReview).
/// </summary>
public sealed class ClaimInformationRequest
{
    private ClaimInformationRequest()
    {
        Title = string.Empty;
        Message = string.Empty;
        RequestedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ClaimId { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public string RequestedByUserId { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }

    public bool IsAnswered { get; private set; }

    public string? ResponseText { get; private set; }

    public string? RespondedByUserId { get; private set; }

    public DateTime? RespondedAtUtc { get; private set; }

    internal static ClaimInformationRequest Create(
        Guid claimId,
        string requestedByUserId,
        string title,
        string message,
        DateTime requestedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(requestedByUserId))
            throw new ArgumentException("User id is required.", nameof(requestedByUserId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Information request title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Information request message is required.", nameof(message));

        return new ClaimInformationRequest
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            Title = title.Trim(),
            Message = message.Trim(),
            RequestedByUserId = requestedByUserId.Trim(),
            RequestedAtUtc = requestedAtUtc,
            IsAnswered = false
        };
    }

    internal void Answer(string respondedByUserId, string responseText, DateTime respondedAtUtc)
    {
        if (IsAnswered)
            throw new InvalidOperationException("This information request has already been answered.");

        if (string.IsNullOrWhiteSpace(respondedByUserId))
            throw new ArgumentException("User id is required.", nameof(respondedByUserId));

        if (string.IsNullOrWhiteSpace(responseText))
            throw new ArgumentException("A response is required.", nameof(responseText));

        IsAnswered = true;
        ResponseText = responseText.Trim();
        RespondedByUserId = respondedByUserId.Trim();
        RespondedAtUtc = respondedAtUtc;
    }
}
