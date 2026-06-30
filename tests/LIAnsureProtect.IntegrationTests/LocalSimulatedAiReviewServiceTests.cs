using LIAnsureProtect.Modules.Underwriting.Application.Ai;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Ai;

namespace LIAnsureProtect.IntegrationTests;

public sealed class LocalSimulatedAiReviewServiceTests
{
    [Fact]
    public async Task GenerateUnderwritingReviewAsync_Returns_Structured_Advisory_Output()
    {
        var service = new LocalSimulatedAiReviewService();
        var request = new AiReviewProviderRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            18_000m,
            5_000_000m,
            10_000m,
            "Severe",
            "Referred",
            "HighRiskCyber",
            ["MFA evidence required."],
            ["Severe risk tier requires underwriter review."],
            [],
            AiReviewConstants.PromptVersion,
            AiReviewConstants.OutputSchemaVersion,
            new DateTime(2026, 6, 22, 1, 0, 0, DateTimeKind.Utc));

        var result = await service.GenerateUnderwritingReviewAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Local Simulated AI", result.ProviderName);
        Assert.Contains("Severe", result.ExecutiveSummary);
        Assert.Contains(result.ControlGaps, gap => gap.Contains("Identity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.SuggestedUnderwritingQuestions, question => question.Contains("MFA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("quote.riskTier", result.Citations);
        Assert.Equal(AiReviewConstants.AdvisoryDisclaimer, result.AdvisoryDisclaimer);
    }
}
