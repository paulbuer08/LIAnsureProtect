namespace LIAnsureProtect.Application.Quotes.Commands.GenerateAiUnderwritingReview;

public sealed record GenerateAiUnderwritingReviewResult(
    Guid ReviewId,
    Guid QuoteId,
    Guid SubmissionId,
    string Status,
    string ProviderName,
    string PromptVersion,
    string OutputSchemaVersion,
    string InputSnapshotHash,
    string? ExecutiveSummary,
    IReadOnlyCollection<string> PositiveRiskSignals,
    IReadOnlyCollection<string> NegativeRiskSignals,
    IReadOnlyCollection<string> ControlGaps,
    IReadOnlyCollection<string> SuggestedUnderwritingQuestions,
    IReadOnlyCollection<string> SuggestedSubjectivityCandidates,
    IReadOnlyCollection<string> Citations,
    IReadOnlyCollection<string> Limitations,
    string? AdvisoryDisclaimer,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime CompletedAtUtc);
