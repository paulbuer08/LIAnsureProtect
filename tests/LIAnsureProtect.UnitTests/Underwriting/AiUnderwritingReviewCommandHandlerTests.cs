using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Ai;
using LIAnsureProtect.Modules.Underwriting.Application.Commands.GenerateAiUnderwritingReview;
using LIAnsureProtect.Modules.Underwriting.Domain;
using LIAnsureProtect.Platform.Abstractions.Security;
using Moq;

namespace LIAnsureProtect.UnitTests.Underwriting;

public sealed class AiUnderwritingReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_Persists_Advisory_Review_From_A_Read_Only_Quote_Snapshot()
    {
        var quote = CreateReferredContext();
        var contextReader = new Mock<IUnderwritingQuoteContextReader>();
        contextReader
            .Setup(reader => reader.GetForAiReviewAsync(quote.QuoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        AiUnderwritingReview? savedReview = null;
        var reviewRepository = new Mock<IAiUnderwritingReviewRepository>();
        reviewRepository
            .Setup(repository => repository.AddAsync(It.IsAny<AiUnderwritingReview>(), It.IsAny<CancellationToken>()))
            .Callback<AiUnderwritingReview, CancellationToken>((review, _) => savedReview = review)
            .Returns(Task.CompletedTask);

        var aiReviewService = new Mock<IAiReviewService>();
        aiReviewService
            .Setup(service => service.GenerateUnderwritingReviewAsync(
                It.IsAny<AiReviewProviderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiReviewProviderResult.Succeeded(
                "Local Simulated AI",
                "The referred risk needs human review for ransomware controls.",
                ["Experienced security team identified."],
                ["Severe risk tier and open MFA evidence subjectivity."],
                ["Identity and access management evidence gap."],
                ["Can the applicant provide MFA rollout evidence?"],
                ["MFA evidence required before bind."],
                ["quote.riskTier", "quote.referralReasons"],
                ["No document evidence was reviewed."],
                AiReviewConstants.AdvisoryDisclaimer,
                new DateTime(2026, 6, 22, 1, 5, 0, DateTimeKind.Utc)));

        var handler = new GenerateAiUnderwritingReviewCommandHandler(
            contextReader.Object,
            aiReviewService.Object,
            reviewRepository.Object,
            new TestCurrentUser("underwriter-1"));

        var result = await handler.Handle(
            new GenerateAiUnderwritingReviewCommand(quote.QuoteId),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(savedReview);
        Assert.Equal(AiUnderwritingReviewStatus.Succeeded, savedReview.Status);
        Assert.Equal("underwriter-1", savedReview.RequestedByUserId);
        Assert.Equal(quote.SubmissionId, result.SubmissionId);
        Assert.Contains("ransomware controls", result.ExecutiveSummary);
        Assert.Contains("quote.riskTier", result.Citations);
        Assert.Equal(AiReviewConstants.AdvisoryDisclaimer, result.AdvisoryDisclaimer);

        // Structural guardrail: the module only READS a quote snapshot through the port — it has no
        // reference to the Quote aggregate, so it cannot approve, decline, adjust, accept, or bind.
        contextReader.Verify(
            reader => reader.GetForAiReviewAsync(quote.QuoteId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Returns_Null_When_The_Quote_Is_Not_Found()
    {
        var contextReader = new Mock<IUnderwritingQuoteContextReader>();
        contextReader
            .Setup(reader => reader.GetForAiReviewAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UnderwritingQuoteContext?)null);
        var reviewRepository = new Mock<IAiUnderwritingReviewRepository>();

        var handler = new GenerateAiUnderwritingReviewCommandHandler(
            contextReader.Object,
            Mock.Of<IAiReviewService>(),
            reviewRepository.Object,
            new TestCurrentUser("underwriter-1"));

        var result = await handler.Handle(
            new GenerateAiUnderwritingReviewCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        reviewRepository.Verify(
            repository => repository.AddAsync(It.IsAny<AiUnderwritingReview>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Rejects_A_Quote_That_Is_Not_Referred()
    {
        var quote = CreateReferredContext() with { Status = "Quoted" };
        var contextReader = new Mock<IUnderwritingQuoteContextReader>();
        contextReader
            .Setup(reader => reader.GetForAiReviewAsync(quote.QuoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);
        var reviewRepository = new Mock<IAiUnderwritingReviewRepository>();

        var handler = new GenerateAiUnderwritingReviewCommandHandler(
            contextReader.Object,
            Mock.Of<IAiReviewService>(),
            reviewRepository.Object,
            new TestCurrentUser("underwriter-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new GenerateAiUnderwritingReviewCommand(quote.QuoteId),
            TestContext.Current.CancellationToken));

        reviewRepository.Verify(
            repository => repository.AddAsync(It.IsAny<AiUnderwritingReview>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static UnderwritingQuoteContext CreateReferredContext()
    {
        return new UnderwritingQuoteContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            Premium: 18_000m,
            RequestedLimit: 5_000_000m,
            Retention: 10_000m,
            RiskTier: "Severe",
            Status: "Referred",
            StrategyName: "HighRiskCyber",
            Subjectivities: ["MFA evidence required."],
            ReferralReasons: ["Severe risk tier requires underwriter review."],
            PriorUnderwritingDecisions: []);
    }

    private sealed class TestCurrentUser(string userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public string? UserId { get; } = userId;

        public string? Email => "underwriter@example.com";

        public IReadOnlyCollection<string> GetRoles() => ["Underwriter"];

        public bool IsInRole(string role) => string.Equals(role, "Underwriter", StringComparison.Ordinal);
    }
}
