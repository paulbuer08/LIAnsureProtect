namespace LIAnsureProtect.Application.Quotes.Ai;

public sealed record AiReviewProviderResult(
    string ProviderName,
    bool IsSuccessful,
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
    DateTime CompletedAtUtc)
{
    public static AiReviewProviderResult Succeeded(
        string providerName,
        string executiveSummary,
        IReadOnlyCollection<string> positiveRiskSignals,
        IReadOnlyCollection<string> negativeRiskSignals,
        IReadOnlyCollection<string> controlGaps,
        IReadOnlyCollection<string> suggestedUnderwritingQuestions,
        IReadOnlyCollection<string> suggestedSubjectivityCandidates,
        IReadOnlyCollection<string> citations,
        IReadOnlyCollection<string> limitations,
        string advisoryDisclaimer,
        DateTime completedAtUtc)
    {
        return new AiReviewProviderResult(
            providerName,
            true,
            executiveSummary,
            positiveRiskSignals,
            negativeRiskSignals,
            controlGaps,
            suggestedUnderwritingQuestions,
            suggestedSubjectivityCandidates,
            citations,
            limitations,
            advisoryDisclaimer,
            FailureReason: null,
            completedAtUtc);
    }

    public static AiReviewProviderResult Failed(
        string providerName,
        string failureReason,
        DateTime completedAtUtc)
    {
        return new AiReviewProviderResult(
            providerName,
            false,
            ExecutiveSummary: null,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            AdvisoryDisclaimer: null,
            failureReason,
            completedAtUtc);
    }
}
