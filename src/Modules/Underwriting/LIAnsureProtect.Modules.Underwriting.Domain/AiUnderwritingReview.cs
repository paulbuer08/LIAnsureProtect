namespace LIAnsureProtect.Modules.Underwriting.Domain;

/// <summary>
/// Advisory AI underwriting review audit record. It references the quote by id only (no navigation/FK
/// into the Quoting context's tables) — the modular-monolith rule. Owned by the Underwriting module.
/// </summary>
public sealed class AiUnderwritingReview
{
    // The only constructor: EF Core materializes through it, and the static factories assign
    // state via the private property setters — no 20+ parameter constructor to maintain.
    private AiUnderwritingReview()
    {
        RequestedByUserId = string.Empty;
        ProviderName = string.Empty;
        PromptVersion = string.Empty;
        OutputSchemaVersion = string.Empty;
        InputSnapshotHash = string.Empty;
        PositiveRiskSignals = "[]";
        NegativeRiskSignals = "[]";
        ControlGaps = "[]";
        SuggestedUnderwritingQuestions = "[]";
        SuggestedSubjectivityCandidates = "[]";
        Citations = "[]";
        Limitations = "[]";
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public string RequestedByUserId { get; private set; }

    public string ProviderName { get; private set; }

    public AiUnderwritingReviewStatus Status { get; private set; }

    public string PromptVersion { get; private set; }

    public string OutputSchemaVersion { get; private set; }

    public string InputSnapshotHash { get; private set; }

    public string? ExecutiveSummary { get; private set; }

    public string PositiveRiskSignals { get; private set; }

    public string NegativeRiskSignals { get; private set; }

    public string ControlGaps { get; private set; }

    public string SuggestedUnderwritingQuestions { get; private set; }

    public string SuggestedSubjectivityCandidates { get; private set; }

    public string Citations { get; private set; }

    public string Limitations { get; private set; }

    public string? AdvisoryDisclaimer { get; private set; }

    public string? FailureReason { get; private set; }

    public AiUnderwritingReviewFeedback Feedback { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime CompletedAtUtc { get; private set; }

    public static AiUnderwritingReview Succeeded(
        Guid quoteId,
        string requestedByUserId,
        string providerName,
        string promptVersion,
        string outputSchemaVersion,
        string inputSnapshotHash,
        string executiveSummary,
        string positiveRiskSignals,
        string negativeRiskSignals,
        string controlGaps,
        string suggestedUnderwritingQuestions,
        string suggestedSubjectivityCandidates,
        string citations,
        string limitations,
        string advisoryDisclaimer,
        DateTime createdAtUtc,
        DateTime completedAtUtc)
    {
        ValidateSharedInputs(quoteId, requestedByUserId, providerName, promptVersion, outputSchemaVersion, inputSnapshotHash);

        if (string.IsNullOrWhiteSpace(executiveSummary))
            throw new ArgumentException("Executive summary is required.", nameof(executiveSummary));

        if (string.IsNullOrWhiteSpace(advisoryDisclaimer))
            throw new ArgumentException("Advisory disclaimer is required.", nameof(advisoryDisclaimer));

        return new AiUnderwritingReview
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            RequestedByUserId = requestedByUserId.Trim(),
            ProviderName = providerName.Trim(),
            Status = AiUnderwritingReviewStatus.Succeeded,
            PromptVersion = promptVersion.Trim(),
            OutputSchemaVersion = outputSchemaVersion.Trim(),
            InputSnapshotHash = inputSnapshotHash.Trim(),
            ExecutiveSummary = executiveSummary.Trim(),
            PositiveRiskSignals = positiveRiskSignals,
            NegativeRiskSignals = negativeRiskSignals,
            ControlGaps = controlGaps,
            SuggestedUnderwritingQuestions = suggestedUnderwritingQuestions,
            SuggestedSubjectivityCandidates = suggestedSubjectivityCandidates,
            Citations = citations,
            Limitations = limitations,
            AdvisoryDisclaimer = advisoryDisclaimer.Trim(),
            FailureReason = null,
            Feedback = AiUnderwritingReviewFeedback.Unrated,
            CreatedAtUtc = createdAtUtc,
            CompletedAtUtc = completedAtUtc
        };
    }

    public static AiUnderwritingReview Failed(
        Guid quoteId,
        string requestedByUserId,
        string providerName,
        string promptVersion,
        string outputSchemaVersion,
        string inputSnapshotHash,
        string failureReason,
        DateTime createdAtUtc,
        DateTime completedAtUtc)
    {
        ValidateSharedInputs(quoteId, requestedByUserId, providerName, promptVersion, outputSchemaVersion, inputSnapshotHash);

        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));

        return new AiUnderwritingReview
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            RequestedByUserId = requestedByUserId.Trim(),
            ProviderName = providerName.Trim(),
            Status = AiUnderwritingReviewStatus.Failed,
            PromptVersion = promptVersion.Trim(),
            OutputSchemaVersion = outputSchemaVersion.Trim(),
            InputSnapshotHash = inputSnapshotHash.Trim(),
            ExecutiveSummary = null,
            AdvisoryDisclaimer = null,
            FailureReason = failureReason.Trim(),
            Feedback = AiUnderwritingReviewFeedback.Unrated,
            CreatedAtUtc = createdAtUtc,
            CompletedAtUtc = completedAtUtc
        };
    }

    private static void ValidateSharedInputs(
        Guid quoteId,
        string requestedByUserId,
        string providerName,
        string promptVersion,
        string outputSchemaVersion,
        string inputSnapshotHash)
    {
        if (quoteId == Guid.Empty)
            throw new ArgumentException("Quote id is required.", nameof(quoteId));

        if (string.IsNullOrWhiteSpace(requestedByUserId))
            throw new ArgumentException("Requested by user id is required.", nameof(requestedByUserId));

        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        if (string.IsNullOrWhiteSpace(promptVersion))
            throw new ArgumentException("Prompt version is required.", nameof(promptVersion));

        if (string.IsNullOrWhiteSpace(outputSchemaVersion))
            throw new ArgumentException("Output schema version is required.", nameof(outputSchemaVersion));

        if (string.IsNullOrWhiteSpace(inputSnapshotHash))
            throw new ArgumentException("Input snapshot hash is required.", nameof(inputSnapshotHash));
    }
}
