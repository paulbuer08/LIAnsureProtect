using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Application.Quotes.Ai;
using LIAnsureProtect.Application.Quotes.Commands.GenerateAiUnderwritingReview;
using LIAnsureProtect.Domain.Quotes;
using Moq;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class AiUnderwritingReviewCommandHandlerTests
{
    [Fact]
    public async Task Handle_Stores_Advisory_Review_Without_Mutating_Quote()
    {
        var quote = CreateReferredQuote();
        var quoteRepository = new Mock<IQuoteRepository>();
        AiUnderwritingReview? savedReview = null;
        quoteRepository
            .Setup(repository => repository.GetForUnderwritingReviewAsync(
                quote.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);
        quoteRepository
            .Setup(repository => repository.ListUnderwritingReviewsAsync(
                quote.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        quoteRepository
            .Setup(repository => repository.AddAiUnderwritingReviewAsync(
                It.IsAny<AiUnderwritingReview>(),
                It.IsAny<CancellationToken>()))
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
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var handler = new GenerateAiUnderwritingReviewCommandHandler(
            quoteRepository.Object,
            aiReviewService.Object,
            unitOfWork.Object,
            new TestCurrentUser("underwriter-1"));

        var result = await handler.Handle(
            new GenerateAiUnderwritingReviewCommand(quote.Id),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(savedReview);
        Assert.Equal(QuoteStatus.Referred, quote.Status);
        Assert.Equal(18_000m, quote.Premium);
        Assert.Equal(10_000m, quote.Retention);
        Assert.Null(quote.ReviewedByUserId);
        Assert.Equal(AiUnderwritingReviewStatus.Succeeded, savedReview.Status);
        Assert.Contains("ransomware controls", result.ExecutiveSummary);
        Assert.Contains("quote.riskTier", result.Citations);
        Assert.Equal(AiReviewConstants.AdvisoryDisclaimer, result.AdvisoryDisclaimer);
    }

    private static Quote CreateReferredQuote()
    {
        return Quote.Generate(
            Guid.NewGuid(),
            "customer-1",
            premium: 18_000m,
            requestedLimit: 5_000_000m,
            retention: 10_000m,
            CyberRiskTier.Severe,
            "HighRiskCyber",
            ["MFA evidence required."],
            ["Severe risk tier requires underwriter review."],
            new DateTime(2026, 6, 22, 1, 0, 0, DateTimeKind.Utc));
    }

    private sealed class TestCurrentUser(string userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public string? UserId { get; } = userId;

        public string? Email => "underwriter@example.com";

        public IReadOnlyCollection<string> GetRoles()
        {
            return ["Underwriter"];
        }

        public bool IsInRole(string role)
        {
            return string.Equals(role, "Underwriter", StringComparison.Ordinal);
        }
    }
}
